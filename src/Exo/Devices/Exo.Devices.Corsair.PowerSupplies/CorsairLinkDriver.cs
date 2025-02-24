using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Cooling;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Cooling;
using Exo.Features.Sensors;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Corsair.PowerSupplies;

public sealed partial class CorsairLinkDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<ICoolingDeviceFeature>,
	ICoolingControllerFeature,
	IDeviceDriver<ISensorDeviceFeature>,
	IDeviceIdFeature,
	ISensorsFeature,
	ISensorsGroupedQueryFeature
{
	private abstract class Sensor
	{
		public CorsairLinkDriver Driver { get; }
		private readonly Guid _sensorId;
		public CorsairPmBusCommand Command { get; }
		public sbyte Page { get; }

		protected Sensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page)
		{
			Driver = driver;
			_sensorId = sensorId;
			Command = command;
			Page = page;
		}

		public Guid SensorId => _sensorId;

		public bool IsGroupQueryEnabled { get; set; }

		public GroupedQueryMode GroupedQueryMode => IsGroupQueryEnabled ? GroupedQueryMode.Enabled : GroupedQueryMode.Supported;

		public virtual SensorUnit Unit => SensorUnit.None;

		public abstract ValueTask GroupedQueryValueAsync(CancellationToken cancellationToken);
	}

	private abstract class Sensor<T> : Sensor, IPolledSensor<T>
		where T : struct, INumber<T>
	{
		private T _lastValue;

		protected Sensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page)
			: base(driver, sensorId, command, page)
		{
		}

		public virtual T? ScaleMinimumValue => null;
		public virtual T? ScaleMaximumValue => null;

		public ValueTask<T> GetValueAsync(CancellationToken cancellationToken)
			=> GroupedQueryMode == GroupedQueryMode.Enabled ?
				ValueTask.FromResult(_lastValue) :
				QueryValueAsync(cancellationToken);

		private async ValueTask<T> QueryValueAsync(CancellationToken cancellationToken)
		{
			await using (await Driver._corsairLinkGuardMutex.AcquireAsync().ConfigureAwait(false))
			{
				if (Page >= 0)
				{
					await Driver._transport.WriteByteAsync(0x00, (byte)Page, cancellationToken);
				}
				return await QueryValueWithinPageAsync(cancellationToken);
			}
		}

		public bool TryGetLastValue(out T lastValue)
		{
			lastValue = _lastValue;
			return true;
		}

		public sealed override async ValueTask GroupedQueryValueAsync(CancellationToken cancellationToken)
			=> _lastValue = await QueryValueWithinPageAsync(cancellationToken);

		protected abstract ValueTask<T> QueryValueWithinPageAsync(CancellationToken cancellationToken);
	}

	private abstract class ByteSensor : Sensor<byte>
	{
		protected ByteSensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page) : base(driver, sensorId, command, page)
		{
		}

		protected override async ValueTask<byte> QueryValueWithinPageAsync(CancellationToken cancellationToken)
			=> await Driver._transport.ReadByteAsync(Command, cancellationToken);
	}

	private abstract class Linear11Sensor : Sensor<float>
	{
		protected Linear11Sensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page) : base(driver, sensorId, command, page)
		{
		}

		protected override async ValueTask<float> QueryValueWithinPageAsync(CancellationToken cancellationToken)
			=> (float)await Driver._transport.ReadLinear11Async(Command, cancellationToken);
	}

	private sealed class TemperatureSensor : Linear11Sensor
	{
		public TemperatureSensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override SensorUnit Unit => SensorUnit.Celsius;
	}

	private sealed class VoltageSensor : Linear11Sensor
	{
		public VoltageSensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override SensorUnit Unit => SensorUnit.Volts;
	}

	private sealed class CurrentSensor : Linear11Sensor
	{
		public CurrentSensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override SensorUnit Unit => SensorUnit.Amperes;
	}

	private sealed class PowerSensor : Linear11Sensor
	{
		public PowerSensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override SensorUnit Unit => SensorUnit.Watts;
	}

	private sealed class PercentSensor : ByteSensor
	{
		public PercentSensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override SensorUnit Unit => SensorUnit.Percent;
	}

	private sealed class RpmSensor : Linear11Sensor
	{
		public RpmSensor(CorsairLinkDriver driver, Guid sensorId, CorsairPmBusCommand command, sbyte page, byte queryOrder) : base(driver, sensorId, command, page)
		{
		}

		public override SensorUnit Unit => SensorUnit.RotationsPerMinute;
	}

	private sealed class FanCooler : ICooler, IAutomaticCooler, IManualCooler
	{
		private readonly CorsairLinkDriver _driver;

		public FanCooler(CorsairLinkDriver driver) => _driver = driver;

		public Guid CoolerId => FanCoolerId;
		public Guid? SpeedSensorId => FanSpeedSensor;
		public CoolerType Type => CoolerType.Fan;
		public CoolingMode CoolingMode => (_driver._currentFanStatus >>> 8) != 0 ? CoolingMode.Manual : CoolingMode.Automatic;

		public byte MinimumPower => 0;
		public byte MaximumPower => 100;
		public bool CanSwitchOff => true;

		public void SwitchToAutomaticCooling() => _driver._currentFanStatus &= 0x00FF;

		public void SetPower(byte power) => _driver._currentFanStatus = (ushort)(0x0100 | power);

		public bool TryGetPower(out byte power)
		{
			ushort status = _driver._currentFanStatus;
			if ((status >>> 8) != 0)
			{
				power = (byte)status;
				return true;
			}
			else
			{
				power = 0;
				return false;
			}
		}
	}

	private const ushort CorsairVendorId = 0x1B1C;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[ProductId(VendorIdSource.Usb, CorsairVendorId, 0x1C08)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILoggerFactory loggerFactory,
		ImmutableArray<SystemDevicePath> keys,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		if (devices.Length != 2) throw new InvalidOperationException("Expected exactly two devices.");
		if (deviceInterfaces.Length != 2) throw new InvalidOperationException("Expected exactly two device interfaces.");

		string? deviceName = null;
		foreach (var deviceInterface in deviceInterfaces)
		{
			if (deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid) && interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				deviceName = deviceInterface.Id;
			}
		}

		if (deviceName is null) throw new InvalidOperationException("HID device interface not found.");

		var stream = new HidFullDuplexStream(deviceName);
		CorsairLinkHidTransport transport;
		try
		{
			var corsairLinkGuardMutex = AsyncGlobalMutex.Get("Global\\CorsairLinkReadWriteGuardMutex");
			string friendlyName;
			bool isManualFanControlEnabled;
			byte fanPower;
			await using (await corsairLinkGuardMutex.AcquireAsync(false))
			{
				transport = await CorsairLinkHidTransport.CreateAsync(loggerFactory.CreateLogger<CorsairLinkHidTransport>(), stream, cancellationToken);
				friendlyName = await transport.ReadStringAsync(CorsairPmBusCommand.ManufacturerModel, cancellationToken);
				isManualFanControlEnabled = await transport.ReadByteAsync(CorsairPmBusCommand.FanMode, cancellationToken) != 0x00;
				fanPower = await transport.ReadByteAsync(CorsairPmBusCommand.FanCommand1, cancellationToken);
			}
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new CorsairLinkDriver
				(
					loggerFactory.CreateLogger<CorsairLinkDriver>(),
					transport,
					corsairLinkGuardMutex,
					isManualFanControlEnabled,
					fanPower,
					productId,
					version,
					friendlyName,
					new DeviceConfigurationKey("Corsair", topLevelDeviceName, $"{CorsairVendorId:X4}{productId:X4}", null)
				)
			);
		}
		catch
		{
			await stream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private readonly CorsairLinkHidTransport _transport;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<ICoolingDeviceFeature> _coolingFeatures;
	private readonly ISensor[] _sensors;
	private readonly ICooler[] _coolers;
	private readonly AsyncGlobalMutex _corsairLinkGuardMutex;
	private readonly ILogger<CorsairLinkDriver> _logger;
	private int _groupQueriedSensorCount;
	private ushort _lastFanStatus;
	private ushort _currentFanStatus;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;

	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(CorsairVendorId, _productId, _versionNumber);

	public override DeviceCategory DeviceCategory => DeviceCategory.PowerSupply;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => ImmutableCollectionsMarshal.AsImmutableArray(_sensors);
	ImmutableArray<ICooler> ICoolingControllerFeature.Coolers => ImmutableCollectionsMarshal.AsImmutableArray(_coolers);

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	IDeviceFeatureSet<ICoolingDeviceFeature> IDeviceDriver<ICoolingDeviceFeature>.Features => _coolingFeatures;

	private CorsairLinkDriver
	(
		ILogger<CorsairLinkDriver> logger,
		CorsairLinkHidTransport transport,
		AsyncGlobalMutex corsairLinkGuardMutex,
		bool isManualFanControlEnabled,
		byte fanPower,
		ushort productId,
		ushort versionNumber,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
		_logger = logger;
		_currentFanStatus = _lastFanStatus = (ushort)((isManualFanControlEnabled ? 0x0100 : 0) | fanPower);
		_productId = productId;
		_versionNumber = versionNumber;
		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, CorsairLinkDriver, IDeviceIdFeature>(this);
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, CorsairLinkDriver, ISensorsFeature, ISensorsGroupedQueryFeature>(this);
		_coolingFeatures = FeatureSet.Create<ICoolingDeviceFeature, CorsairLinkDriver, ICoolingControllerFeature>(this);
		byte order = 0;
		_sensors =
		[
			new TemperatureSensor(this, TemperatureSensor1, CorsairPmBusCommand.Temperature1, -1, order++),
			new TemperatureSensor(this, TemperatureSensor2, CorsairPmBusCommand.Temperature2, -1, order++),

			new RpmSensor(this, FanSpeedSensor, CorsairPmBusCommand.ReadFanSpeed1, -1, order++),

			new VoltageSensor(this, InputVoltageSensor, CorsairPmBusCommand.ReadVoltageIn, -1, order++),
			new PowerSensor(this, OutputPowerSensor, CorsairPmBusCommand.ReadGlobalPowerOut, -1, order++),

			new VoltageSensor(this, PowerRail3VoltageSensor, CorsairPmBusCommand.ReadVoltageOut, 2, order++),
			new CurrentSensor(this, PowerRail3CurrentSensor, CorsairPmBusCommand.ReadIntensityOut, 2, order++),
			new PowerSensor(this, PowerRail3PowerSensor, CorsairPmBusCommand.ReadPowerOut, 2, order++),

			new VoltageSensor(this, PowerRail2VoltageSensor, CorsairPmBusCommand.ReadVoltageOut, 1, order++),
			new CurrentSensor(this, PowerRail2CurrentSensor, CorsairPmBusCommand.ReadIntensityOut, 1, order++),
			new PowerSensor(this, PowerRail2PowerSensor, CorsairPmBusCommand.ReadPowerOut, 1, order++),

			new VoltageSensor(this, PowerRail1VoltageSensor, CorsairPmBusCommand.ReadVoltageOut, 0, order++),
			new CurrentSensor(this, PowerRail1CurrentSensor, CorsairPmBusCommand.ReadIntensityOut, 0, order++),
			new PowerSensor(this, PowerRail1PowerSensor, CorsairPmBusCommand.ReadPowerOut, 0, order++),
		];
		_coolers =
		[
			new FanCooler(this),
		];
		_corsairLinkGuardMutex = corsairLinkGuardMutex;
		_productId = productId;
		_versionNumber = versionNumber;
	}

	public override async ValueTask DisposeAsync()
	{
		await _transport.DisposeAsync().ConfigureAwait(false);
	}

	void ISensorsGroupedQueryFeature.AddSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new ArgumentException();
		if (!s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = true;
			_groupQueriedSensorCount++;
		}
	}

	void ISensorsGroupedQueryFeature.RemoveSensor(IPolledSensor sensor)
	{
		if (sensor is not Sensor s || s.Driver != this) throw new ArgumentException();
		if (s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = false;
			_groupQueriedSensorCount--;
		}
	}

	async ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	{
		if (_groupQueriedSensorCount == 0) return;

		await using (await _corsairLinkGuardMutex.AcquireAsync().ConfigureAwait(false))
		{
			sbyte currentSetPage = -1;
			foreach (var sensor in _sensors)
			{
				var s = Unsafe.As<Sensor>(sensor);
				if (s.GroupedQueryMode != GroupedQueryMode.Enabled) continue;

				if (currentSetPage != s.Page)
				{
					currentSetPage = s.Page;
					if (currentSetPage >= 0)
					{
						// NB: The write page command should generally have better synchronization purposes related to the protocol, but we could still read a stale response…
						await _transport.WriteByteAsync(0x00, (byte)currentSetPage, cancellationToken);
					}
				}

				try
				{
					await s.GroupedQueryValueAsync(cancellationToken);
				}
				catch (CorsairLinkReadErrorException)
				{
					// Retry once in case of a read error. (This could happen if HWiNFO64 is running in parallel)
					await s.GroupedQueryValueAsync(cancellationToken);
				}
			}
		}
	}

	async ValueTask ICoolingControllerFeature.ApplyChangesAsync(CancellationToken cancellationToken)
	{
		ushort lastStatus = _lastFanStatus;
		ushort currentStatus = _currentFanStatus;
		ushort statusChanges = (ushort)(lastStatus ^ currentStatus);

		if (statusChanges == 0) return;

		await using (await _corsairLinkGuardMutex.AcquireAsync().ConfigureAwait(false))
		{
			bool modeChange = (statusChanges >> 8) != 0;
			bool isManualFanControlEnabled = (currentStatus >>> 8) != 0;
			if (isManualFanControlEnabled)
			{
				await _transport.WriteByteAsync(CorsairPmBusCommand.FanCommand1, (byte)currentStatus, cancellationToken);
			}
			if (modeChange)
			{
				await _transport.WriteByteAsync(CorsairPmBusCommand.FanMode, isManualFanControlEnabled ? (byte)1 : (byte)0, cancellationToken);
			}
		}

		_lastFanStatus = currentStatus;
	}
}
