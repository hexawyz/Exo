using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.ColorFormats;
using Exo.Cooling;
using Exo.Devices.Nzxt.LightingEffects;
using Exo.Discovery;
using Exo.EmbeddedMonitors;
using Exo.Features;
using Exo.Features.Cooling;
using Exo.Features.EmbeddedMonitors;
using Exo.Features.Lighting;
using Exo.Features.Monitors;
using Exo.Features.Sensors;
using Exo.Images;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Exo.Monitors;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

public partial class KrakenDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<ILightingDeviceFeature>,
	IDeviceDriver<ISensorDeviceFeature>,
	IDeviceDriver<ICoolingDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceDriver<IEmbeddedMonitorDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	ILightingControllerFeature,
	ILightingDeferredChangesFeature,
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
				// We don't need to explicitly reference the stream anymore after this and we can avoid double dispose by signaling it there.
				// (Not that double dispose would actually cause a problem, but it is more correct to not do it)
				hidStream = null;
				try
				{
					var ledChannels = await hidTransport.GetLedInformationAsync(cancellationToken);

					(byte ChannelIndex, ImmutableArray<LightingAccessoryInformation> Accessories)[] ledChannelsAccessories;
					if (ledChannels.Length == 0)
					{
						ledChannelsAccessories = [];
					}
					else
					{
						ledChannelsAccessories = new (byte ChannelIndex, ImmutableArray<LightingAccessoryInformation> Accessories)[ledChannels.Length];
						uint ledChannelCount = 0;
						for (int i = 0; i < ledChannels.Length; i++)
						{
							var channel = ledChannels[i];
							if (channel.Length > 0)
							{
								bool isLightingSupported = true;
								var accessories = new LightingAccessoryInformation[channel.Length];
								for (int j = 0; j < channel.Length; j++)
								{
									byte accessoryId = channel[j];
									if (LightingAccessoryInformation.TryGet(accessoryId, out var info))
									{
										logger.KrakenLightingKnownAccessory(hidDeviceInterfaceName, (byte)(i + 1), (byte)(j + 1), accessoryId, info.Name);
										accessories[j] = info;
									}
									else
									{
										logger.KrakenLightingUnknownAccessory(hidDeviceInterfaceName, (byte)(i + 1), (byte)(j + 1), accessoryId);
										isLightingSupported = false;
									}
								}
								if (isLightingSupported)
								{
									ledChannelsAccessories[ledChannelCount++] = ((byte)(i + 1), ImmutableCollectionsMarshal.AsImmutableArray(accessories));
								}
							}
						}
						if (ledChannelCount != (uint)ledChannelsAccessories.Length)
						{
							Array.Resize(ref ledChannelsAccessories, (int)ledChannelCount);
						}
					}

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
							ImmutableCollectionsMarshal.AsImmutableArray(ledChannelsAccessories),
							displayManager,
							screenInfo.Width,
							screenInfo.Height,
							productId,
							version,
							friendlyName ?? await hidTransport.GetProductNameAsync(cancellationToken).ConfigureAwait(false) ?? "NZXT Kraken",
							new("Kraken", topLevelDeviceName, $"{NzxtVendorId:X4}:{productId:X4}", serialNumber)
						),
						null
					);
				}
				catch
				{
					await hidTransport.DisposeAsync().ConfigureAwait(false);
					hidStream = null;
					throw;
				}
			}
			catch when (hidStream is not null)
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
	private readonly LightingZone[] _lightingZones;
	private readonly ISensor[] _sensors;
	private readonly ICooler[] _coolers;
	private readonly ILogger<KrakenDriver> _logger;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<ICoolingDeviceFeature> _coolingFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;
	private readonly IDeviceFeatureSet<IEmbeddedMonitorDeviceFeature> _embeddedMonitorFeatures;

	private readonly ImmutableArray<EmbeddedMonitorGraphicsDescription> _embeddedMonitorGraphicsDescriptions;

	private readonly byte[] _pumpCoolingCurve;
	private readonly byte[] _fanCoolingCurve;

	private byte _pumpSpeedTarget;
	private byte _pumpState;
	private byte _fanSpeedTarget;
	private byte _fanState;

	private byte _groupQueriedSensorCount;

	private readonly ushort _productId;
	private readonly ushort _versionNumber;
	private readonly ushort _imageWidth;
	private readonly ushort _imageHeight;

	public override DeviceCategory DeviceCategory => DeviceCategory.Cooler;
	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(NzxtVendorId, _productId, _versionNumber);
	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => ImmutableCollectionsMarshal.AsImmutableArray(_sensors);
	ImmutableArray<ICooler> ICoolingControllerFeature.Coolers => ImmutableCollectionsMarshal.AsImmutableArray(_coolers);

	Guid IEmbeddedMonitor.MonitorId => MonitorId;
	EmbeddedMonitorInformation IEmbeddedMonitor.MonitorInformation => new(MonitorShape.Circle, ImageRotation.None, new(_imageWidth, _imageHeight), PixelFormat.R8G8B8X8, ImageFormats.Raw | ImageFormats.Gif, true);

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	IDeviceFeatureSet<ICoolingDeviceFeature> IDeviceDriver<ICoolingDeviceFeature>.Features => _coolingFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	IDeviceFeatureSet<IEmbeddedMonitorDeviceFeature> IDeviceDriver<IEmbeddedMonitorDeviceFeature>.Features => _embeddedMonitorFeatures;

	private KrakenDriver
	(
		ILogger<KrakenDriver> logger,
		KrakenHidTransport transport,
		ImmutableArray<(byte ChannelIndex, ImmutableArray<LightingAccessoryInformation> Accessories)> ledChannels,
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
		if (ledChannels.Length == 0)
		{
			_lightingZones = [];
		}
		else
		{
			var lightingZones = new List<LightingZone>();
			foreach (var (channelId, accessories) in ledChannels)
			{
				for (int i = 0; i < accessories.Length; i++)
				{
					var accessory = accessories[i];
					byte accessoryIndex = (byte)(i + 1);
					lightingZones.Add(new(accessory.GetZoneId(channelId, accessoryIndex), channelId, accessoryIndex, accessory.LedCount));
				}
			}
			_lightingZones = [.. lightingZones];
		}
		_genericFeatures = ConfigurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature>(this);
		_lightingFeatures = _lightingZones.Length > 0 ?
			FeatureSet.Create<ILightingDeviceFeature, KrakenDriver, ILightingControllerFeature, ILightingDeferredChangesFeature>(this) :
			FeatureSet.Empty<ILightingDeviceFeature>();
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

	IReadOnlyCollection<ILightingZone> ILightingControllerFeature.LightingZones => _lightingZones;
	LightingPersistenceMode ILightingDeferredChangesFeature.PersistenceMode => LightingPersistenceMode.NeverPersisted;

	async ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync(bool shouldPersist)
	{
		foreach (var zone in _lightingZones)
		{
			await zone.ApplyEffectAsync(_hidTransport, default).ConfigureAwait(false);
		}
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
		if ((_fanState & CoolingStateCurve) != 0)
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

	private sealed class LightingZone :
		ILightingZone,
		ILightingZoneEffect<DisabledEffect>,
		ILightingZoneEffect<StaticColorEffect>,
		ILightingZoneEffect<ColorPulseEffect>,
		ILightingZoneEffect<VariableColorPulseEffect>,
		ILightingZoneEffect<TaiChiEffect>,
		ILightingZoneEffect<LiquidCoolerEffect>,
		ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>
	{
		const byte DefaultStaticSpeed = 0x32;

		// NB: Takes the speeds from CAM but insert an extra one between "normal" and "fast" in order to match the predetermined model used in Exo.
		// CAM <=> Exo:
		// Slower <=> Slower
		// Slow <=> Slow
		// Normal <=> Medium Slow
		// <nothing> <=> Medium fast
		// Fast <=> Fast
		// Faster <=> Faster
		private static ReadOnlySpan<ushort> PulseSpeeds => [0x19, 0x14, 0x0f, 0xa, 0x07, 0x04];
		private static ReadOnlySpan<ushort> BreathingSpeeds => [0x28, 0x1e, 0x14, 0x0f, 0x0a, 0x04];
		private static ReadOnlySpan<ushort> FadeSpeeds => [0x50, 0x3c, 0x28, 0x1e, 0x14, 0x0a];
		private static ReadOnlySpan<ushort> CoveringBannerSpeeds => [0x015e, 0x012c, 0x00fa, 0x00dc, 0x0096, 0x0050];
		private static ReadOnlySpan<ushort> TaiChiSpeeds => [0x32, 0x28, 0x1e, 0x19, 0x14, 0x0a];
		private static ReadOnlySpan<ushort> LiquidCoolerSpeeds => [0x32, 0x28, 0x1e, 0x19, 0x14, 0x0a];
		private static ReadOnlySpan<ushort> SpectrumWaveSpeeds => [0x015e, 0x012c, 0x00fa, 0x00dc, 0x0096, 0x0050];

		private readonly RgbColor[] _colors;
		private readonly Guid _zoneId;
		// NB: As in most types in Exo, fields are ordered to reduce the amount of padding as much as possible.
		private readonly byte _channelId;
		private readonly byte _accessoryId;
		private readonly byte _ledCount;
		private KrakenEffect _effectId;
		private ushort _speed;
		private byte _colorCount;
		private byte _flags;
		private byte _parameter2;
		private byte _size;
		private bool _hasChanged;

		public LightingZone(Guid zoneId, byte channelId, byte accessoryId, byte colorCount)
		{
			_zoneId = zoneId;
			_channelId = channelId;
			_accessoryId = accessoryId;
			_ledCount = colorCount;
			_colors = new RgbColor[Math.Max(8, (uint)colorCount)];
			_effectId = KrakenEffect.Static;
			_colorCount = 1;
			_speed = 0x32;
			_size = 0x03;
		}

		Guid ILightingZone.ZoneId => _zoneId;

		ILightingEffect ILightingZone.GetCurrentEffect()
		{
			int speedIndex;
			switch (_effectId)
			{
			case KrakenEffect.Static:
				return _colors[0] == default ? DisabledEffect.SharedInstance : new StaticColorEffect(_colors[0]);
			case KrakenEffect.SpectrumWave:
				return new ReversibleVariableSpectrumWaveEffect((speedIndex = SpectrumWaveSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & 0x02) != 0);
			case KrakenEffect.Pulse:
				return (speedIndex = PulseSpeeds.IndexOf(_speed)) >= 0 ? new VariableColorPulseEffect(_colors[0], (PredeterminedEffectSpeed)speedIndex) : new ColorPulseEffect(_colors[0]);
			case KrakenEffect.TaiChi:
				return new TaiChiEffect(_colors[0], _colors[1], (speedIndex = TaiChiSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & 0x02) != 0);
			case KrakenEffect.LiquidCooler:
				return new LiquidCoolerEffect(_colors[0], _colors[1], (speedIndex = LiquidCoolerSpeeds.IndexOf(_speed)) >= 0 ? (PredeterminedEffectSpeed)speedIndex : PredeterminedEffectSpeed.MediumSlow, (_flags & 0x02) != 0);
			default:
				throw new NotImplementedException();
			}
		}

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect)
		{
			if (_effectId != KrakenEffect.Static || _colorCount != 1 || _colors[0] != default || _speed != DefaultStaticSpeed || _flags != 0x00 && _parameter2 != 0x00 || _size != 0x03)
			{
				_effectId = KrakenEffect.Static;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 1;
				_speed = DefaultStaticSpeed;
				_flags = 0x00;
				_parameter2 = 0x00;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect)
		{
			if (_effectId != KrakenEffect.Static || _colorCount != 1 || _colors[0] != effect.Color || _speed != DefaultStaticSpeed || _flags != 0x00 && _parameter2 != 0x00 || _size != 0x03)
			{
				_effectId = KrakenEffect.Static;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = DefaultStaticSpeed;
				_flags = 0x00;
				_parameter2 = 0x00;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(in ColorPulseEffect effect)
		{
			if (_effectId != KrakenEffect.Pulse || _colorCount != 1 || _colors[0] != effect.Color || _speed != PulseSpeeds[2] || _flags != 0x00 && _parameter2 != 0x08 || _size != 0x03)
			{
				_effectId = KrakenEffect.Pulse;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = PulseSpeeds[2];
				_flags = 0x00;
				_parameter2 = 0x08;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<VariableColorPulseEffect>.ApplyEffect(in VariableColorPulseEffect effect)
		{
			if (_effectId != KrakenEffect.Pulse || _colorCount != 1 || _colors[0] != effect.Color || _speed != PulseSpeeds[2] || _flags != 0x00 && _parameter2 != 0x08 || _size != 0x03)
			{
				_effectId = KrakenEffect.Pulse;
				_colors[0] = effect.Color;
				if (_colorCount > 1)
				{
					_colors.AsSpan(1, _colorCount - 1).Clear();
				}
				_colorCount = 1;
				_speed = PulseSpeeds[(int)effect.Speed];
				_flags = 0x00;
				_parameter2 = 0x08;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<TaiChiEffect>.ApplyEffect(in TaiChiEffect effect)
		{
			if (_effectId != KrakenEffect.TaiChi ||
				_colorCount != 2 ||
				_colors[0] != effect.Color1 ||
				_colors[1] != effect.Color2 ||
				TaiChiSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.IsReversed ? (byte)0x02 : (byte)0x00) ||
				_parameter2 != 0x05 ||
				_size != 0x03)
			{
				_effectId = KrakenEffect.TaiChi;
				_colors[0] = effect.Color1;
				_colors[1] = effect.Color2;
				if (_colorCount > 2)
				{
					_colors.AsSpan(2, _colorCount - 2).Clear();
				}
				_colorCount = 2;
				_speed = TaiChiSpeeds[(int)effect.Speed];
				_flags = effect.IsReversed ? (byte)0x02 : (byte)0x00;
				_parameter2 = 0x05;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<LiquidCoolerEffect>.ApplyEffect(in LiquidCoolerEffect effect)
		{
			if (_effectId != KrakenEffect.LiquidCooler ||
				_colorCount != 2 ||
				_colors[0] != effect.Color1 ||
				_colors[1] != effect.Color2 ||
				LiquidCoolerSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.IsReversed ? (byte)0x02 : (byte)0x00) ||
				_parameter2 != 0x05 ||
				_size != 0x03)
			{
				_effectId = KrakenEffect.LiquidCooler;
				_colors[0] = effect.Color1;
				_colors[1] = effect.Color2;
				if (_colorCount > 2)
				{
					_colors.AsSpan(2, _colorCount - 2).Clear();
				}
				_colorCount = 2;
				_speed = LiquidCoolerSpeeds[(int)effect.Speed];
				_flags = effect.IsReversed ? (byte)0x02 : (byte)0x00;
				_parameter2 = 0x05;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		void ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>.ApplyEffect(in ReversibleVariableSpectrumWaveEffect effect)
		{
			if (_effectId != KrakenEffect.SpectrumWave ||
				_colorCount != 0 ||
				SpectrumWaveSpeeds.IndexOf(_speed) is int speedIndex && (speedIndex < 0 || (PredeterminedEffectSpeed)speedIndex != effect.Speed) ||
				_flags != (effect.IsReversed ? (byte)0x02 : (byte)0x00) ||
				_parameter2 != 0x00 ||
				_size != 0x03)
			{
				_effectId = KrakenEffect.SpectrumWave;
				_colors.AsSpan(0, _colorCount).Clear();
				_colorCount = 2;
				_speed = SpectrumWaveSpeeds[(int)effect.Speed];
				_flags = effect.IsReversed ? (byte)0x02 : (byte)0x00;
				_parameter2 = 0x00;
				_size = 0x03;
				_hasChanged = true;
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect)
		{
			effect = default;
			return _effectId == KrakenEffect.Static && _colorCount == 1 && _colors[0] == default && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == 0x03;
		}

		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect)
		{
			if (_effectId == KrakenEffect.Static && _colorCount == 1 && _speed == DefaultStaticSpeed && _flags == 0x00 && _parameter2 == 0x00 && _size == 0x03)
			{
				effect = new(_colors[0]);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse && _colorCount == 1 && _speed == PulseSpeeds[2] && _flags == 0x00 && _parameter2 == 0x08 && _size == 0x03)
			{
				effect = new(_colors[0]);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<VariableColorPulseEffect>.TryGetCurrentEffect(out VariableColorPulseEffect effect)
		{
			if (_effectId == KrakenEffect.Pulse && _colorCount == 1 && PulseSpeeds.IndexOf(_speed) is int speedIndex && speedIndex >= 0 && _flags == 0x00 && _parameter2 == 0x08 && _size == 0x03)
			{
				effect = new(_colors[0], (PredeterminedEffectSpeed)speedIndex);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<TaiChiEffect>.TryGetCurrentEffect(out TaiChiEffect effect)
		{
			if (_effectId == KrakenEffect.TaiChi &&
				_colorCount == 2 &&
				TaiChiSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is 0x00 or 0x02 &&
				_parameter2 == 0x05 &&
				_size == 0x03)
			{
				effect = new(_colors[0], _colors[1], (PredeterminedEffectSpeed)speedIndex, _flags != 0);
				return true;
			}
			effect = default;
			return false;
		}

		bool ILightingZoneEffect<LiquidCoolerEffect>.TryGetCurrentEffect(out LiquidCoolerEffect effect) => throw new NotImplementedException();

		bool ILightingZoneEffect<ReversibleVariableSpectrumWaveEffect>.TryGetCurrentEffect(out ReversibleVariableSpectrumWaveEffect effect)
		{
			if (_effectId == KrakenEffect.SpectrumWave &&
				_colorCount == 0 &&
				SpectrumWaveSpeeds.IndexOf(_speed) is int speedIndex &&
				speedIndex >= 0 &&
				_flags is 0x00 or 0x02 &&
				_parameter2 == 0x05 &&
				_size == 0x03)
			{
				effect = new((PredeterminedEffectSpeed)speedIndex, _flags != 0);
				return true;
			}
			effect = default;
			return false;
		}

		public bool HasChanged => _hasChanged;

		public async Task ApplyEffectAsync(KrakenHidTransport transport, CancellationToken cancellationToken)
		{
			if (_hasChanged)
			{
				await transport.SetMulticolorEffectAsync(_channelId, (byte)_effectId, _speed, _flags, _parameter2, _ledCount, _size, _colors.AsMemory(0, _colorCount), cancellationToken).ConfigureAwait(false);
				_hasChanged = false;
			}
		}
	}

	// Most effects have an "automatic" and an "addressable" version, so hopefully, we should be able to reference all effects using these "automatic" IDs.
	private enum KrakenEffect
	{
		Static = 0x00,
		Fade = 0x01,
		SpectrumWave = 0x02,
		Marquee = 0x03,
		CoveringMarquee = 0x04,
		Alternating = 0x05,
		Pulse = 0x06,
		Breathing = 0x07,
		Candle = 0x08,
		StarryNight = 0x09,
		Blink = 0x0a,
		RainbowWave = 0x0B,
		SuperRainbow = 0x0C,
		RainbowImpulse = 0x0D,
		TaiChi = 0x0E,
		LiquidCooler = 0x0F,
		Loading = 0x10,
	}
}
