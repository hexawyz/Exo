using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Cooling;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Cooling;
using Exo.Features.EmbeddedMonitors;
using Exo.Features.Monitors;
using Exo.Features.Sensors;
using Exo.Images;
using Exo.Monitors;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

public partial class KrakenDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<ISensorDeviceFeature>,
	IDeviceDriver<ICoolingDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceDriver<IEmbeddedMonitorDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	ISensorsFeature,
	ISensorsGroupedQueryFeature,
	ICoolingControllerFeature,
	IEmbeddedMonitorControllerFeature,
	IEmbeddedMonitorBuiltInGraphics,
	IMonitorBrightnessFeature
{
	// Both cooling curves taken out of NZXT CAM when creating a new cooling profile. No idea if they are the default HW curve.
	// Points from CAM are the first column, others are interpolated.
	private static readonly byte[] DefaultPumpCurve = [
		60, 60, 60, 60, 60,
		60, 60, 60, 60, 60,
		60, 60, 60, 60, 60,
		60, 62, 64, 66, 68,
		70, 72, 74, 76, 78,
		80, 82, 84, 86, 88,
		90, 92, 94, 96, 98,
		100, 100, 100, 100, 100,
	];

	private static readonly byte[] DefaultFanCurve = [
		30, 30, 30, 30, 30,
		30, 30, 30, 30, 30,
		30, 30, 30, 30, 30,
		30, 31, 32, 33, 34,
		35, 36, 38, 39, 40,
		42, 44, 45, 47, 48,
		50, 51, 53, 54, 55,
		57, 58, 59, 61, 62,
	];

	private static readonly ImmutableArray<byte> DefaultControlCurveInputValues = [20, 25, 30, 35, 40, 45, 50, 55, 59];

	private const int NzxtVendorId = 0x1E71;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	// Kraken Z
	[ProductId(VendorIdSource.Usb, NzxtVendorId, 0x3008)]
	// Kraken Elite (2023)
	[ProductId(VendorIdSource.Usb, NzxtVendorId, 0x300C)]
	// Kraken (2023)
	//[ProductId(VendorIdSource.Usb, NzxtVendorId, 0x300E)]
	// Kraken Elite (2024)
	[ProductId(VendorIdSource.Usb, NzxtVendorId, 0x3012)]
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
			throw new MissingKernelDriverException(friendlyName, DeviceId.ForUsb(NzxtVendorId, productId, version));
		}

		// Now strictly require the WinUSB device to be present.
		// Some operations are possible without it, but it makes no sense now that the device is properly supported.
		DeviceStream winUsbDevice;
		try
		{
			winUsbDevice = new DeviceStream(Device.OpenHandle(winUsbDeviceInterfaceName, DeviceAccess.ReadWrite), FileAccess.ReadWrite, 0, true);
		}
		catch (UnauthorizedAccessException)
		{
			logger.KrakenWinUsbDeviceLocked(winUsbDeviceInterfaceName);
			throw;
		}
		try
		{
			var imageTransport = await KrakenWinUsbImageTransport.CreateAsync(winUsbDevice, cancellationToken).ConfigureAwait(false);
			var hidStream = new HidFullDuplexStream(hidDeviceInterfaceName);
			try
			{
				string? serialNumber = await hidStream.GetSerialNumberAsync(cancellationToken).ConfigureAwait(false);
				var hidTransport = new KrakenHidTransport(hidStream);
				var screenInfo = await hidTransport.GetScreenInformationAsync(cancellationToken).ConfigureAwait(false);
				var displayManager = await KrakenDisplayManager.CreateAsync(screenInfo.ImageCount, screenInfo.MemoryBlockCount, hidTransport, imageTransport, cancellationToken).ConfigureAwait(false);

				await hidTransport.SetPumpPowerCurveAsync(DefaultPumpCurve, cancellationToken).ConfigureAwait(false);
				await hidTransport.SetFanPowerCurveAsync(DefaultFanCurve, cancellationToken).ConfigureAwait(false);

				// Forcefully reset the display mode if we are currently displaying an image.
				// While it is possible to allow the current image to continue existing, disabling it is a quick and easy way to avoid memory management problems.
				// There is generally no merit in preserving the previous image state of the device, as we sadly can't know which image is stored where.
				// This means that restarting the service would essentially duplicate the current image in another slot. Which is kinda… stupid.
				// We can allow that later once we have perfected the memory management and slots can be deallocated in a smarted way.
				if (displayManager.CurrentDisplayMode.DisplayMode == KrakenDisplayMode.StoredImage)
				{
					// Counting on the fact that we would quickly update the display mode after this.
					await displayManager.DisplayPresetVisualAsync(KrakenPresetVisual.Off, cancellationToken).ConfigureAwait(false);
				}

				return new DriverCreationResult<SystemDevicePath>
				(
					keys,
					new KrakenDriver
					(
						logger,
						hidTransport,
						displayManager,
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
		catch when (winUsbDevice is not null)
		{
			await winUsbDevice.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private const byte CoolingStateCurve = 0b01;
	private const byte CoolingStateChanged = 0b10;

	private readonly KrakenHidTransport _hidTransport;
	private readonly KrakenDisplayManager _displayManager;
	private readonly ISensor[] _sensors;
	private readonly ICooler[] _coolers;
	private readonly ILogger<KrakenDriver> _logger;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<ICoolingDeviceFeature> _coolingFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;
	private readonly IDeviceFeatureSet<IEmbeddedMonitorDeviceFeature> _embeddedMonitorFeatures;

	private byte _pumpSpeedTarget;
	private byte _pumpState;
	private byte _fanSpeedTarget;
	private byte _fanState;

	private byte _groupQueriedSensorCount;

	private readonly ushort _productId;
	private readonly ushort _versionNumber;
	private readonly ushort _imageWidth;
	private readonly ushort _imageHeight;

	private readonly byte[] _pumpCoolingCurve;
	private readonly byte[] _fanCoolingCurve;

	private readonly ImmutableArray<EmbeddedMonitorGraphicsDescription> _embeddedMonitorGraphicsDescriptions;

	public override DeviceCategory DeviceCategory => DeviceCategory.Cooler;
	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(NzxtVendorId, _productId, _versionNumber);
	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => ImmutableCollectionsMarshal.AsImmutableArray(_sensors);
	ImmutableArray<ICooler> ICoolingControllerFeature.Coolers => ImmutableCollectionsMarshal.AsImmutableArray(_coolers);

	Guid IEmbeddedMonitor.MonitorId => MonitorId;
	EmbeddedMonitorInformation IEmbeddedMonitor.MonitorInformation => new(MonitorShape.Circle, ImageRotation.None, new(_imageWidth, _imageHeight), PixelFormat.R8G8B8X8, ImageFormats.Raw | ImageFormats.Gif, true);

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	IDeviceFeatureSet<ICoolingDeviceFeature> IDeviceDriver<ICoolingDeviceFeature>.Features => _coolingFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	IDeviceFeatureSet<IEmbeddedMonitorDeviceFeature> IDeviceDriver<IEmbeddedMonitorDeviceFeature>.Features => _embeddedMonitorFeatures;

	private KrakenDriver
	(
		ILogger<KrakenDriver> logger,
		KrakenHidTransport transport,
		KrakenDisplayManager displayManager,
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
		_hidTransport = transport;
		_displayManager = displayManager;
		_productId = productId;
		_versionNumber = versionNumber;
		_pumpCoolingCurve = (byte[])DefaultPumpCurve.Clone();
		_fanCoolingCurve = (byte[])DefaultFanCurve.Clone();
		_sensors = [new LiquidTemperatureSensor(this), new PumpSpeedSensor(this), new FanSpeedSensor(this)];
		_coolers = [new PumpCooler(this), new FanCooler(this)];
		_imageWidth = imageWidth;
		_imageHeight = imageHeight;
		_embeddedMonitorGraphicsDescriptions =
		[
			EmbeddedMonitorGraphicsDescription.Off,
			new(BootAnimationGraphicsId),
			new(LiquidTemperatureGraphicsId, new Guid(0x5553C264, 0x35BF, 0x44BA, 0xBD, 0x23, 0x5A, 0x1B, 0xF6, 0x11, 0xF5, 0xE1)),
			EmbeddedMonitorGraphicsDescription.CustomGraphics,
		];
		_genericFeatures = ConfigurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature>(this);
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, KrakenDriver, ISensorsFeature, ISensorsGroupedQueryFeature>(this);
		_coolingFeatures = FeatureSet.Create<ICoolingDeviceFeature, KrakenDriver, ICoolingControllerFeature>(this);
		_monitorFeatures = FeatureSet.Create<IMonitorDeviceFeature, KrakenDriver, IMonitorBrightnessFeature>(this);
		_embeddedMonitorFeatures = FeatureSet.Create<IEmbeddedMonitorDeviceFeature, KrakenDriver, IEmbeddedMonitorControllerFeature>(this);
	}

	public override async ValueTask DisposeAsync()
	{
		await _hidTransport.DisposeAsync().ConfigureAwait(false);
		if (_displayManager is not null) await _displayManager.DisposeAsync().ConfigureAwait(false);
	}

	async ValueTask<ContinuousValue> IContinuousVcpFeature.GetValueAsync(CancellationToken cancellationToken)
	{
		var info = await _hidTransport.GetScreenInformationAsync(cancellationToken).ConfigureAwait(false);
		return new ContinuousValue(info.CurrentBrightness, 0, 100);
	}

	async ValueTask IContinuousVcpFeature.SetValueAsync(ushort value, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
		await _hidTransport.SetBrightnessAsync((byte)value, cancellationToken).ConfigureAwait(false);
	}

	ImmutableArray<IEmbeddedMonitor> IEmbeddedMonitorControllerFeature.EmbeddedMonitors => [this];

	ImmutableArray<EmbeddedMonitorGraphicsDescription> IEmbeddedMonitorBuiltInGraphics.SupportedGraphics => _embeddedMonitorGraphicsDescriptions;

	Guid IEmbeddedMonitorBuiltInGraphics.CurrentGraphicsId
		=> _displayManager.CurrentDisplayMode.DisplayMode switch
		{
			KrakenDisplayMode.Off => EmbeddedMonitorGraphicsDescription.OffId,
			KrakenDisplayMode.Animation => BootAnimationGraphicsId,
			KrakenDisplayMode.LiquidTemperature => LiquidTemperatureGraphicsId,
			KrakenDisplayMode.StoredImage => default,
			// Fallback to reporting the monitor as being off.
			_ => EmbeddedMonitorGraphicsDescription.OffId
		};

	async ValueTask IEmbeddedMonitorBuiltInGraphics.SetCurrentModeAsync(Guid modeId, CancellationToken cancellationToken)
	{
		KrakenPresetVisual presetVisual;
		if (modeId == EmbeddedMonitorGraphicsDescription.OffId) presetVisual = KrakenPresetVisual.Off;
		else if (modeId == BootAnimationGraphicsId) presetVisual = KrakenPresetVisual.Animation;
		else if (modeId == LiquidTemperatureGraphicsId) presetVisual = KrakenPresetVisual.LiquidTemperature;
		else throw new ArgumentException();
		await _displayManager.DisplayPresetVisualAsync(presetVisual, cancellationToken).ConfigureAwait(false);
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
		if (_groupQueriedSensorCount == 0) return;

		var readings = await _hidTransport.GetRecentReadingsAsync(cancellationToken).ConfigureAwait(false);

		foreach (var sensor in _sensors)
		{
			Unsafe.As<Sensor>(sensor).RefreshValue(readings);
		}
	}

	async ValueTask ICoolingControllerFeature.ApplyChangesAsync(CancellationToken cancellationToken)
	{
		ValueTask pumpSetTask = ValueTask.CompletedTask;
		ValueTask fanSetTask = ValueTask.CompletedTask;

		if ((_pumpState & CoolingStateChanged) != 0) pumpSetTask = UpdatePumpPowerAsync(cancellationToken);
		if ((_fanState & CoolingStateChanged) != 0) fanSetTask = UpdateFanPowerAsync(cancellationToken);

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

	private async ValueTask UpdatePumpPowerAsync(CancellationToken cancellationToken)
	{
		if ((_pumpState & CoolingStateCurve) != 0)
		{
			await _hidTransport.SetPumpPowerCurveAsync(_pumpCoolingCurve, cancellationToken).ConfigureAwait(false);
			_pumpState = CoolingStateCurve;
		}
		else
		{
			await _hidTransport.SetPumpPowerAsync(_pumpSpeedTarget, cancellationToken).ConfigureAwait(false);
			_pumpState = 0;
		}
	}

	private async ValueTask UpdateFanPowerAsync(CancellationToken cancellationToken)
	{
		if ((_pumpState & CoolingStateCurve) != 0)
		{
			await _hidTransport.SetFanPowerCurveAsync(_fanCoolingCurve, cancellationToken).ConfigureAwait(false);
			_fanState = CoolingStateCurve;
		}
		else
		{
			await _hidTransport.SetFanPowerAsync(_fanSpeedTarget, cancellationToken).ConfigureAwait(false);
			_fanState = 0;
		}
	}

	// TODO: Make the image allocation algorithm smarter.
	// The version here is a MVP and may fail to display images on some occasions.
	// Ideally, we need to be able to deallocate other images to free up memory when needed.
	// But we also need to have a LRU cache to avoid evicting images that would be switched to often.
	// And for that, we need to also track the image IDs that are currently assigned.
	// Although it doesn't feel that clean, maybe just making the ImageStorageManager handle everything related to display (so a DisplayManager) is the best solution.
	async ValueTask IEmbeddedMonitor.SetImageAsync(UInt128 imageId, ImageFormat imageFormat, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
		=> await _displayManager.DisplayImageAsync
		(
			imageId,
			imageFormat switch
			{
				ImageFormat.Gif => KrakenImageFormat.Gif,
				ImageFormat.Raw => KrakenImageFormat.Raw,
				_ => throw new ArgumentOutOfRangeException(nameof(imageFormat)),
			},
			data,
			cancellationToken
		);

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

	// TODO: Expose the sensor as float16 instead. Half is not optimized yet, but when it is finally (.NET 10?), it will be more interesting.
	private sealed class LiquidTemperatureSensor : Sensor<float>
	{
		public override Guid SensorId => LiquidTemperatureSensorId;
		public override SensorUnit Unit => SensorUnit.Celsius;
		public override float? ScaleMaximumValue => 100;

		public LiquidTemperatureSensor(KrakenDriver driver) : base(driver) { }

		protected override float ReadValue(KrakenReadings readings) => readings.LiquidTemperature + readings.LiquidTemperatureDecimal / 10f;
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

	private static void SetControlCurve(byte[] dest, ref byte state, IControlCurve<byte, byte> curve)
	{
		for (int i = 0; i < dest.Length; i++)
		{
			dest[i] = curve[(byte)(20 + i)];
		}
		state = CoolingStateChanged | CoolingStateCurve;
	}

	public static bool TryGetControlCurve(byte[] src, byte state, [NotNullWhen(true)] out IControlCurve<byte, byte>? curve)
	{
		if ((state & CoolingStateCurve) != 0)
		{
			var dataPoints = new DataPoint<byte, byte>[src.Length];
			for (int i = 0; i < src.Length; i++)
			{
				dataPoints[i] = new((byte)(20 + i), src[i]);
			}
			curve = new InterpolatedSegmentControlCurve<byte, byte>(ImmutableCollectionsMarshal.AsImmutableArray(dataPoints), MonotonicityValidators<byte>.IncreasingUpTo100);
			return true;
		}
		curve = null;
		return false;
	}

	private sealed class FanCooler : ICooler, IManualCooler, IHardwareCurveCooler, IHardwareCurveCoolerSensorCurveControl<byte>
	{
		private readonly KrakenDriver _driver;
		private readonly ImmutableArray<IHardwareCurveCoolerSensorCurveControl> _availableSensors;

		public FanCooler(KrakenDriver driver)
		{
			_driver = driver;
			_availableSensors = [this];
		}

		public Guid CoolerId => FanCoolerId;
		public CoolerType Type => CoolerType.Fan;

		public Guid? SpeedSensorId => FanSpeedSensorId;
		public CoolingMode CoolingMode => CoolingMode.Manual;

		Guid IHardwareCurveCoolerSensorCurveControl.SensorId => LiquidTemperatureSensorId;
		SensorUnit IHardwareCurveCoolerSensorCurveControl.Unit => SensorUnit.Celsius;

		public byte MinimumInputValue => 20;
		public byte MaximumInputValue => 59;
		public ImmutableArray<byte> DefaultCurveInputValues => DefaultControlCurveInputValues;

		// NB: From testing, the speed starts increasing at 21%, for about 500 RPM.
		public byte MinimumPower => 20;
		public byte MaximumPower => 100;
		public bool CanSwitchOff => true;

		public void SetPower(byte power)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(power, 100);
			_driver._fanSpeedTarget = power;
			_driver._fanState = CoolingStateChanged;
		}

		public bool TryGetPower(out byte power)
		{
			if ((_driver._fanState & CoolingStateCurve) == 0)
			{
				power = _driver._fanSpeedTarget;
				return true;
			}
			power = 0;
			return false;
		}

		public ImmutableArray<IHardwareCurveCoolerSensorCurveControl> AvailableSensors => _availableSensors;

		public bool TryGetActiveSensor([NotNullWhen(true)] out IHardwareCurveCoolerSensorCurveControl? sensor)
		{
			if ((_driver._fanState & CoolingStateCurve) != 0)
			{
				sensor = this;
				return true;
			}
			sensor = null;
			return false;
		}

		public void SetControlCurve(IControlCurve<byte, byte> curve)
			=> KrakenDriver.SetControlCurve(_driver._fanCoolingCurve, ref _driver._fanState, curve);

		public bool TryGetControlCurve([NotNullWhen(true)] out IControlCurve<byte, byte>? curve)
			=> KrakenDriver.TryGetControlCurve(_driver._fanCoolingCurve, _driver._fanState, out curve);
	}

	private sealed class PumpCooler : ICooler, IManualCooler, IHardwareCurveCooler, IHardwareCurveCoolerSensorCurveControl<byte>
	{
		private readonly KrakenDriver _driver;
		private readonly ImmutableArray<IHardwareCurveCoolerSensorCurveControl> _availableSensors;

		public PumpCooler(KrakenDriver driver)
		{
			_driver = driver;
			_availableSensors = [this];
		}

		public Guid CoolerId => PumpCoolerId;
		public CoolerType Type => CoolerType.Pump;

		public Guid? SpeedSensorId => PumpSpeedSensorId;
		public CoolingMode CoolingMode => CoolingMode.Manual;

		Guid IHardwareCurveCoolerSensorCurveControl.SensorId => LiquidTemperatureSensorId;
		SensorUnit IHardwareCurveCoolerSensorCurveControl.Unit => SensorUnit.Celsius;

		public byte MinimumInputValue => 20;
		public byte MaximumInputValue => 59;
		public ImmutableArray<byte> DefaultCurveInputValues => DefaultControlCurveInputValues;

		// NB: From testing, the speed starts increasing at 32%, and the effective minimum speed seems to map to about 41% of the maximum. (~1150 RPM / ~2800 RPM)
		public byte MinimumPower => 30;
		public byte MaximumPower => 100;
		public bool CanSwitchOff => false;

		public void SetPower(byte power)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(power, 100);
			_driver._pumpSpeedTarget = power;
			_driver._pumpState = CoolingStateChanged;
		}

		public bool TryGetPower(out byte power)
		{
			if ((_driver._pumpState & CoolingStateCurve) == 0)
			{
				power = _driver._pumpSpeedTarget;
				return true;
			}
			power = 0;
			return false;
		}

		public ImmutableArray<IHardwareCurveCoolerSensorCurveControl> AvailableSensors => _availableSensors;

		public bool TryGetActiveSensor([NotNullWhen(true)] out IHardwareCurveCoolerSensorCurveControl? sensor)
		{
			if ((_driver._pumpState & CoolingStateCurve) != 0)
			{
				sensor = this;
				return true;
			}
			sensor = null;
			return false;
		}

		public void SetControlCurve(IControlCurve<byte, byte> curve)
			=> KrakenDriver.SetControlCurve(_driver._pumpCoolingCurve, ref _driver._pumpState, curve);

		public bool TryGetControlCurve([NotNullWhen(true)] out IControlCurve<byte, byte>? curve)
			=> KrakenDriver.TryGetControlCurve(_driver._pumpCoolingCurve, _driver._pumpState, out curve);
	}
}
