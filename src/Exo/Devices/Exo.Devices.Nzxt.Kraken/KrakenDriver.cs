using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.WinUsb;
using Exo.Cooling;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Cooling;
using Exo.Features.Monitors;
using Exo.Features.Sensors;
using Exo.Images;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

public class KrakenDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	IDeviceDriver<ISensorDeviceFeature>,
	ISensorsFeature,
	ISensorsGroupedQueryFeature,
	IDeviceDriver<ICoolingDeviceFeature>,
	ICoolingControllerFeature,
	IDeviceDriver<IMonitorDeviceFeature>,
	IEmbeddedMonitorInformationFeature,
	IMonitorBrightnessFeature
{
	private static readonly Guid LiquidTemperatureSensorId = new(0x8E880DE1, 0x2A45, 0x400D, 0xA9, 0x0F, 0x42, 0xE8, 0x9B, 0xF9, 0x50, 0xDB);
	private static readonly Guid PumpSpeedSensorId = new(0x3A2F0F14, 0x3957, 0x400E, 0x8B, 0x6C, 0xCB, 0x02, 0x5B, 0x89, 0x15, 0x06);
	private static readonly Guid FanSpeedSensorId = new(0xFDC93D5B, 0xEDE3, 0x4774, 0x96, 0xEC, 0xC4, 0xFD, 0xB1, 0xC1, 0xDE, 0xBC);

	private static readonly Guid FanCoolerId = new(0x5A0FE6F5, 0xB7D1, 0x46E4, 0xA5, 0x12, 0x82, 0x72, 0x6E, 0x95, 0x35, 0xC4);
	private static readonly Guid PumpCoolerId = new(0x2A57C838, 0xCD58, 0x4D6C, 0xAF, 0x9E, 0xF5, 0xBD, 0xDD, 0x6F, 0xB9, 0x92);

	private const int NzxtVendorId = 0x1E71;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	[ProductId(VendorIdSource.Usb, NzxtVendorId, 0x3008)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<KrakenDriver> logger,
		ImmutableArray<SystemDevicePath> keys,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string friendlyName,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		if (deviceInterfaces.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four device interfaces.");
		}

		if (devices.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four devices.");
		}

		string? hidDeviceInterfaceName = null;
		string? winUsbDeviceInterfaceName = null;
		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid))
			{
				continue;
			}

			if (interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				hidDeviceInterfaceName = deviceInterface.Id;
				//HidDevice.FromPath(hidDeviceInterfaceName);
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.WinUsb)
			{
				winUsbDeviceInterfaceName = deviceInterface.Id;
			}
		}

		if (hidDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("The Kraken HID device interface was not found.");
		}

		if (winUsbDeviceInterfaceName is null)
		{
			throw new MissingKernelDriverException(friendlyName);
		}

		//var winUsbDevice = new DeviceStream(Device.OpenHandle(winUsbDeviceInterfaceName, DeviceAccess.ReadWrite), FileAccess.ReadWrite, 0, true);
		//var deviceDescriptor = await winUsbDevice.GetDeviceDescriptorAsync(cancellationToken).ConfigureAwait(false);
		//var configuration = await winUsbDevice.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
		var hidStream = new HidFullDuplexStream(hidDeviceInterfaceName);
		try
		{
			string? serialNumber = await hidStream.GetSerialNumberAsync(cancellationToken).ConfigureAwait(false);
			var transport = new KrakenHidTransport(hidStream);
			var screenInfo = await transport.GetScreenInformationAsync(cancellationToken).ConfigureAwait(false);
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new KrakenDriver
				(
					logger,
					transport,
					screenInfo.Width,
					screenInfo.Height,
					productId,
					version,
					friendlyName,
					new("Kraken", topLevelDeviceName, $"{NzxtVendorId:X4}:{productId:X4}", serialNumber)
				),
				null
			);
		}
		catch
		{
			await hidStream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private readonly KrakenHidTransport _transport;
	private readonly ISensor[] _sensors;
	private readonly ICooler[] _coolers;
	private readonly ILogger<KrakenDriver> _logger;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<ICoolingDeviceFeature> _coolingFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;

	private int _groupQueriedSensorCount;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;
	private readonly ushort _imageWidth;
	private readonly ushort _imageHeight;

	private byte _lastPumpSpeedTarget;
	private byte _currentPumpSpeedTarget;
	private byte _lastFanSpeedTarget;
	private byte _currentFanSpeedTarget;

	public override DeviceCategory DeviceCategory => DeviceCategory.Cooler;
	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(NzxtVendorId, _productId, _versionNumber);
	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => ImmutableCollectionsMarshal.AsImmutableArray(_sensors);
	ImmutableArray<ICooler> ICoolingControllerFeature.Coolers => ImmutableCollectionsMarshal.AsImmutableArray(_coolers);

	MonitorShape IEmbeddedMonitorInformationFeature.Shape => MonitorShape.Circle;

	Size IEmbeddedMonitorInformationFeature.ImageSize => new(_imageWidth, _imageHeight);

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	IDeviceFeatureSet<ICoolingDeviceFeature> IDeviceDriver<ICoolingDeviceFeature>.Features => _coolingFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	private KrakenDriver
	(
		ILogger<KrakenDriver> logger,
		KrakenHidTransport transport,
		ushort imageWidth,
		ushort imageHeight,
		ushort productId,
		ushort versionNumber,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	)
		: base(friendlyName, configurationKey)
	{
		_logger = logger;
		_transport = transport;
		_productId = productId;
		_versionNumber = versionNumber;
		_sensors = [new LiquidTemperatureSensor(this), new PumpSpeedSensor(this), new FanSpeedSensor(this)];
		_coolers = [new PumpCooler(this), new FanCooler(this)];
		_genericFeatures = ConfigurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature>(this);
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, KrakenDriver, ISensorsFeature, ISensorsGroupedQueryFeature>(this);
		_coolingFeatures = FeatureSet.Create<ICoolingDeviceFeature, KrakenDriver, ICoolingControllerFeature>(this);
		_monitorFeatures = FeatureSet.Create<IMonitorDeviceFeature, KrakenDriver, IEmbeddedMonitorInformationFeature, IMonitorBrightnessFeature>(this);
	}

	public override ValueTask DisposeAsync() => _transport.DisposeAsync();

	async ValueTask<ContinuousValue> IContinuousVcpFeature.GetValueAsync(CancellationToken cancellationToken)
	{
		var info = await _transport.GetScreenInformationAsync(cancellationToken).ConfigureAwait(false);
		return new ContinuousValue(info.CurrentBrightness, 0, 100);
	}

	async ValueTask IContinuousVcpFeature.SetValueAsync(ushort value, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
		await _transport.SetBrightnessAsync((byte)value, cancellationToken).ConfigureAwait(false);
	}

	void ISensorsGroupedQueryFeature.AddSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new InvalidOperationException();

		if (!s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = true;
			_groupQueriedSensorCount++;
		}
	}

	void ISensorsGroupedQueryFeature.RemoveSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new InvalidOperationException();

		if (s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = false;
			_groupQueriedSensorCount--;
		}
	}

	async ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	{
		var readings = await _transport.GetRecentReadingsAsync(cancellationToken).ConfigureAwait(false);

		foreach (var sensor in _sensors)
		{
			Unsafe.As<Sensor>(sensor).RefreshValue(readings);
		}
	}

	async ValueTask ICoolingControllerFeature.ApplyChangesAsync(CancellationToken cancellationToken)
	{
		ValueTask pumpSetTask = ValueTask.CompletedTask;
		ValueTask fanSetTask = ValueTask.CompletedTask;

		if (_lastPumpSpeedTarget != _currentPumpSpeedTarget) pumpSetTask = UpdatePumpPowerAsync(_currentPumpSpeedTarget, cancellationToken);
		if (_lastFanSpeedTarget != _currentFanSpeedTarget) fanSetTask = UpdateFanPowerAsync(_currentFanSpeedTarget, cancellationToken);

		List<Exception>? exceptions = null;
		bool operationCanceled = false;
		try
		{
			await pumpSetTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			operationCanceled = true;
		}
		catch (Exception ex)
		{
			exceptions = new(2) { ex };
		}
		try
		{
			await fanSetTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			operationCanceled = true;
		}
		catch (Exception ex)
		{
			exceptions ??= new(1);
			exceptions.Add(ex);
		}
		if (exceptions is { Count: > 0 })
		{
			throw new AggregateException([.. exceptions]);
		}
		if (operationCanceled)
		{
			cancellationToken.ThrowIfCancellationRequested();
		}
	}

	private async ValueTask UpdatePumpPowerAsync(byte power, CancellationToken cancellationToken)
	{
		await _transport.SetPumpPowerAsync(power, cancellationToken).ConfigureAwait(false);
		_lastPumpSpeedTarget = power;
	}

	private async ValueTask UpdateFanPowerAsync(byte power, CancellationToken cancellationToken)
	{
		await _transport.SetFanPowerAsync(power, cancellationToken).ConfigureAwait(false);
		_lastFanSpeedTarget = power;
	}

	private abstract class Sensor
	{
		private readonly KrakenDriver _driver;

		protected Sensor(KrakenDriver driver) => _driver = driver;

		public KrakenDriver Driver => _driver;

		public abstract bool IsGroupQueryEnabled { get; set; }

		public abstract void RefreshValue(KrakenReadings readings);
	}

	private abstract class Sensor<T> : Sensor, IPolledSensor<T>
		where T : struct, INumber<T>
	{
		private T _lastValue;
		private bool _isGroupQueryEnabled;

		protected Sensor(KrakenDriver driver) : base(driver) { }

		public T? ScaleMinimumValue => default(T);
		public virtual T? ScaleMaximumValue => null;
		public abstract Guid SensorId { get; }
		public abstract SensorUnit Unit { get; }
		public sealed override bool IsGroupQueryEnabled { get => _isGroupQueryEnabled; set => _isGroupQueryEnabled = value; }
		public GroupedQueryMode GroupedQueryMode => _isGroupQueryEnabled ? GroupedQueryMode.Enabled : GroupedQueryMode.Supported;

		public sealed override void RefreshValue(KrakenReadings readings)
			=> _lastValue = ReadValue(readings);

		protected abstract T ReadValue(KrakenReadings readings);

		public ValueTask<T> GetValueAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_lastValue);

		public bool TryGetLastValue(out T lastValue)
		{
			lastValue = _lastValue;
			return true;
		}
	}

	private sealed class LiquidTemperatureSensor : Sensor<byte>
	{
		public override Guid SensorId => LiquidTemperatureSensorId;
		public override SensorUnit Unit => SensorUnit.Celsius;
		public override byte? ScaleMaximumValue => 100;

		public LiquidTemperatureSensor(KrakenDriver driver) : base(driver) { }

		protected override byte ReadValue(KrakenReadings readings) => readings.LiquidTemperature;
	}

	private sealed class PumpSpeedSensor : Sensor<ushort>
	{
		public override Guid SensorId => PumpSpeedSensorId;
		public override SensorUnit Unit => SensorUnit.RotationsPerMinute;

		public PumpSpeedSensor(KrakenDriver driver) : base(driver) { }

		protected override ushort ReadValue(KrakenReadings readings) => readings.PumpSpeed;
	}

	private sealed class FanSpeedSensor : Sensor<ushort>
	{
		public override Guid SensorId => FanSpeedSensorId;
		public override SensorUnit Unit => SensorUnit.RotationsPerMinute;

		public FanSpeedSensor(KrakenDriver driver) : base(driver) { }

		protected override ushort ReadValue(KrakenReadings readings) => readings.FanSpeed;
	}

	private sealed class FanCooler : ICooler, IManualCooler
	{
		private readonly KrakenDriver _driver;

		public FanCooler(KrakenDriver driver) => _driver = driver;

		public Guid CoolerId => FanCoolerId;
		public CoolerType Type => CoolerType.Fan;

		public Guid? SpeedSensorId => FanSpeedSensorId;
		public CoolingMode CoolingMode => CoolingMode.Manual;

		// NB: From testing, the speed starts increasing at 21%, for about 500 RPM.
		public byte MinimumPower => 20;
		public byte MaximumPower => 100;
		public bool CanSwitchOff => true;

		public void SetPower(byte power)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(power, 100);
			_driver._currentFanSpeedTarget = power;
		}

		public bool TryGetPower(out byte power)
		{
			power = _driver._currentFanSpeedTarget;
			return true;
		}
	}

	private sealed class PumpCooler : ICooler, IManualCooler
	{
		private readonly KrakenDriver _driver;

		public PumpCooler(KrakenDriver driver) => _driver = driver;

		public Guid CoolerId => PumpCoolerId;
		public CoolerType Type => CoolerType.Pump;

		public Guid? SpeedSensorId => PumpSpeedSensorId;
		public CoolingMode CoolingMode => CoolingMode.Manual;

		// NB: From testing, the speed starts increasing at 32%, and the effective minimum speed seems to map to about 41% of the maximum. (~1150 RPM / ~2800 RPM)
		public byte MinimumPower => 30;
		public byte MaximumPower => 100;
		public bool CanSwitchOff => false;

		public void SetPower(byte power)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(power, 100);
			_driver._currentPumpSpeedTarget = power;
		}

		public bool TryGetPower(out byte power)
		{
			power = _driver._currentPumpSpeedTarget;
			return true;
		}
	}
}
