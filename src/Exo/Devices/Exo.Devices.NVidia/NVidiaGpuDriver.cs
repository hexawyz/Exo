using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.DisplayDevices;
using Exo.ColorFormats;
using Exo.Cooling;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Cooling;
using Exo.Features.Lighting;
using Exo.Features.Sensors;
using Exo.I2C;
using Exo.Lighting;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver :
	Driver,
	IDeviceIdFeature,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IDisplayAdapterDeviceFeature>,
	IDeviceDriver<ILightingDeviceFeature>,
	IDeviceDriver<ISensorDeviceFeature>,
	IDeviceDriver<ICoolingDeviceFeature>,
	IDisplayAdapterI2cBusProviderFeature,
	ILightingControllerFeature,
	ILightingDeferredChangesFeature,
	ISensorsFeature,
	ISensorsGroupedQueryFeature
{
	private abstract class GroupQueriedSensor
	{
		protected GroupQueriedSensor(NvApi.PhysicalGpu gpu) => Gpu = gpu;

		public NvApi.PhysicalGpu Gpu { get; }

		public bool IsGroupQueryEnabled { get; set; }

		public GroupedQueryMode GroupedQueryMode => IsGroupQueryEnabled ? GroupedQueryMode.Enabled : GroupedQueryMode.Supported;
	}

	private const ushort NVidiaVendorId = 0x10DE;

	private static readonly Guid[] GpuTopZoneIds =
	[
		new(0x22C9ECBE, 0xD047, 0x4E26, 0xB0, 0xF4, 0x73, 0x0E, 0x5B, 0x3E, 0x40, 0x7E),
		new(0x2CCFFD3F, 0x98C9, 0x4030, 0xB0, 0x2D, 0xE3, 0x3F, 0xC3, 0xA9, 0xD8, 0xAE),
		new(0x444912A4, 0x8E08, 0x42F8, 0x86, 0x02, 0x4F, 0xE0, 0xF6, 0x32, 0xEC, 0xD2),
		new(0xC78170B2, 0x639D, 0x452A, 0xAA, 0x80, 0xB4, 0x12, 0x56, 0xC8, 0x00, 0x68),
	];

	private static readonly Guid[] GpuFrontZoneIds =
	[
		new(0x95A687EF, 0x6CBB, 0x4C22, 0xAA, 0xDB, 0x79, 0x18, 0x67, 0x44, 0xAD, 0xDA),
		new(0x0BF2C1E7, 0x8B03, 0x4EDC, 0xBA, 0x37, 0x6C, 0xFF, 0x62, 0x31, 0xB5, 0xCD),
		new(0x4B325C17, 0xA8E6, 0x4D8B, 0x80, 0x56, 0x75, 0xE2, 0xF2, 0x7D, 0xF4, 0xBF),
		new(0x9D7B59E6, 0xFB66, 0x46AA, 0x97, 0x89, 0x5C, 0x09, 0x95, 0x3C, 0x46, 0xBB),
	];

	private static readonly Guid[] GpuBackZoneIds =
	[
		new(0x93505342, 0x3BEE, 0x4C22, 0xA5, 0x1E, 0xDD, 0x29, 0xCC, 0xF6, 0x55, 0xED),
		new(0x44170FAD, 0x08C3, 0x46A2, 0xB4, 0xE2, 0x60, 0xCB, 0xB9, 0x1A, 0x77, 0x68),
		new(0x84E65B9E, 0x6BAA, 0x4559, 0x98, 0x51, 0x93, 0x46, 0x11, 0x58, 0x89, 0xF8),
		new(0x284EBE2F, 0x5E9B, 0x4F6B, 0x8D, 0x86, 0x71, 0x47, 0x85, 0x68, 0x19, 0x5D),
	];

	private static readonly Guid[] SliTopZoneIds =
	[
		new(0x211DA55C, 0xDFCC, 0x4A4D, 0xA9, 0x4E, 0x7F, 0x3C, 0xFA, 0x00, 0xC7, 0x22),
		new(0x84396000, 0x6DA7, 0x4039, 0xB3, 0x2A, 0x6A, 0xF4, 0x45, 0xD3, 0xB7, 0xF1),
		new(0xEEA7A9DC, 0xB811, 0x463F, 0xBA, 0xD5, 0x7D, 0x22, 0x43, 0xB1, 0x0A, 0x03),
		new(0x82658966, 0x39CD, 0x40B7, 0xBA, 0x7C, 0x87, 0xB2, 0x7F, 0xE4, 0xAA, 0x99),
	];

	private static readonly Guid LegacyFanSpeedSensorId = new(0x3428225A, 0x6BE4, 0x44AF, 0xB1, 0xCD, 0x80, 0x0A, 0x55, 0xF9, 0x43, 0x2F);

	private static readonly Guid Fan1SpeedSensorId = new(0xFCF6D2E1, 0x9048, 0x4E87, 0xAC, 0x9F, 0x91, 0xE2, 0x50, 0xE1, 0x21, 0x8B);
	private static readonly Guid Fan2SpeedSensorId = new(0xBE0F57CB, 0xCD6D, 0x4422, 0xA0, 0x24, 0xB2, 0x8F, 0xF4, 0x25, 0xE9, 0x03);

	private static readonly Guid Fan1CoolerId = new(0x1F3E6519, 0x9404, 0x4866, 0x85, 0xB8, 0x52, 0xD4, 0x52, 0x48, 0x50, 0x60);
	private static readonly Guid Fan2CoolerId = new(0xB02259B1, 0xF5DC, 0x4371, 0x9C, 0xD3, 0xBC, 0xE4, 0xBE, 0x34, 0x53, 0x1F);

	private static readonly Guid GraphicsUtilizationSensorId = new(0x005F94DD, 0x09F5, 0x46D3, 0x99, 0x02, 0xE1, 0x5D, 0x6A, 0x19, 0xD8, 0x24);
	private static readonly Guid FrameBufferUtilizationSensorId = new(0xBF9AAD1D, 0xE013, 0x4178, 0x97, 0xB3, 0x42, 0x20, 0xD2, 0x6C, 0xBE, 0x71);
	private static readonly Guid VideoUtilizationSensorId = new(0x147C8F52, 0x1402, 0x4515, 0xB9, 0xFB, 0x41, 0x48, 0xFD, 0x02, 0x12, 0xA4);

	private static readonly Guid GpuThermalSensorId = new(0xB65044BE, 0xA3B0, 0x4EFB, 0x9E, 0xFB, 0x82, 0x6E, 0x3B, 0xD7, 0x60, 0xA0);
	private static readonly Guid MemoryThermalSensorId = new(0x463292D7, 0x4C7F, 0x4848, 0xBA, 0x9E, 0x71, 0x28, 0x21, 0xDF, 0xD5, 0xE8);
	private static readonly Guid PowerSupplyThermalSensorId = new(0x73356F82, 0x4194, 0x46DF, 0x9F, 0x9F, 0x25, 0x19, 0x21, 0x01, 0x5F, 0x81);
	private static readonly Guid BoardThermalSensorId = new(0x359E57BE, 0x577D, 0x46ED, 0xA0, 0x9A, 0xD3, 0xA4, 0x4B, 0x95, 0xCA, 0xE8);

	private static readonly Guid GraphicsFrequencySensorId = new(0xCD415440, 0xFB17, 0x441D, 0xA6, 0x4F, 0x6C, 0x62, 0x73, 0x34, 0x90, 0x00);
	private static readonly Guid MemoryFrequencySensorId = new(0xDE435007, 0xCCD1, 0x414E, 0x8C, 0xF1, 0x22, 0x0F, 0xCC, 0xE4, 0xD0, 0x99);
	private static readonly Guid ProcessorFrequencySensorId = new(0x6E1311E3, 0xD1FB, 0x4555, 0x8C, 0xD9, 0xD0, 0xF3, 0xF6, 0x43, 0x31, 0xEB);
	private static readonly Guid VideoFrequencySensorId = new(0xD9272343, 0x5AC4, 0x4CD8, 0x9F, 0x7A, 0x02, 0xE1, 0x30, 0xC1, 0x5F, 0xD3);

	[DiscoverySubsystem<PciDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayAdapter)]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayDeviceArrival)]
	[VendorId(VendorIdSource.Pci, NVidiaVendorId)]
	public static ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILoggerFactory loggerFactory,
		ImmutableArray<SystemDevicePath> keys,
		DeviceObjectInformation topLevelDevice,
		DeviceId deviceId
	)
	{
		// First, we need identify the expected bus number and address.
		// This will be used to match the device through the NVIDIA API.
		if (!topLevelDevice.Properties.TryGetValue(Properties.System.Devices.BusNumber.Key, out uint busNumber))
		{
			throw new InvalidOperationException($"Could not retrieve the bus number for the device {topLevelDevice.Id}.");
		}
		if (!topLevelDevice.Properties.TryGetValue(Properties.System.Devices.Address.Key, out uint pciAddress))
		{
			throw new InvalidOperationException($"Could not retrieve the bus address for the device {topLevelDevice.Id}.");
		}

		var logger = loggerFactory.CreateLogger<NVidiaGpuDriver>();

		// Initialize the API and log the version.
		logger.NvApiVersion(NvApi.GetInterfaceVersionString());

		NvApi.PhysicalGpu foundGpu = default;

		// Enumerate all the GPUs and find the right one.
		foreach (var gpu in NvApi.GetPhysicalGpus())
		{
			if (gpu.GetBusId() != busNumber || gpu.GetBusSlotId() != pciAddress >> 16) continue;

			foundGpu = gpu;
			break;
		}

		if (!foundGpu.IsValid)
		{
			throw new InvalidOperationException($"Could not find the corresponding GPU through NVAPI for {topLevelDevice.Id}.");
		}

		string friendlyName = foundGpu.GetFullName();
		byte[] serialNumber = foundGpu.GetSerialNumber();

		// It seems that NVAPI doesn't return the GPU Serial Number (anymore?) :(
		//var serialNumber = foundGpu.GetBoardNumber();

		var devices = foundGpu.GetIlluminationDevices();
		var deviceControls = foundGpu.GetIlluminationDeviceControls();
		var zones = foundGpu.GetIlluminationZones();
		var zoneControls = foundGpu.GetIlluminationZoneControls(false);

		if (zones.Length != zoneControls.Length)
		{
			throw new InvalidOperationException("The returned number of zone controls is different from the number of zone informations.");
		}

		var @lock = new Lock();

		var lightingZones = ImmutableArray.CreateBuilder<LightingZone>(zones.Length);

		for (int i = 0; i < zones.Length; i++)
		{
			var zone = zones[i];
			var zoneControl = zoneControls[i];

			var zoneId = zone.Location.Component switch
			{
				NvApi.Gpu.Client.IlluminationZoneLocationComponent.Gpu => zone.Location.Face switch
				{
					NvApi.Gpu.Client.IlluminationZoneLocationFace.Top => GpuTopZoneIds[zone.Location.Index],
					NvApi.Gpu.Client.IlluminationZoneLocationFace.Front => GpuFrontZoneIds[zone.Location.Index],
					NvApi.Gpu.Client.IlluminationZoneLocationFace.Back => GpuBackZoneIds[zone.Location.Index],
					_ => default
				},
				NvApi.Gpu.Client.IlluminationZoneLocationComponent.Sli => zone.Location.Face switch
				{
					NvApi.Gpu.Client.IlluminationZoneLocationFace.Top => SliTopZoneIds[zone.Location.Index],
					_ => default,
				},
				_ => default
			};

			if (zoneId == default)
			{
				logger.IlluminationZoneNotMapped((byte)i, zone.Location.Component, zone.Location.Face, zone.Location.Index);
				continue;
			}

			switch (zone.Type)
			{
			case NvApi.Gpu.Client.IlluminationZoneType.Invalid:
				logger.IlluminationZoneInvalidType((byte)i);
				continue;
			case NvApi.Gpu.Client.IlluminationZoneType.Rgb:
				lightingZones.Add
				(
					new RgbLightingZone
					(
						@lock,
						i,
						zoneId,
						zoneControl.ControlMode == NvApi.Gpu.Client.IlluminationControlMode.Manual ?
							new RgbColor(zoneControl.Data.Rgb.Manual.R, zoneControl.Data.Rgb.Manual.G, zoneControl.Data.Rgb.Manual.B) :
							default
					)
				);
				break;
			case NvApi.Gpu.Client.IlluminationZoneType.ColorFixed:
				lightingZones.Add
				(
					new FixedColorLightingZone
					(
						@lock,
						i,
						zoneId,
						zoneControl.ControlMode == NvApi.Gpu.Client.IlluminationControlMode.Manual ?
							zoneControl.Data.ColorFixed.Manual.BrightnessPercentage :
							default
					)
				);
				break;
			case NvApi.Gpu.Client.IlluminationZoneType.Rgbw:
				lightingZones.Add
				(
					new RgbwLightingZone
					(
						@lock,
						i,
						zoneId,
						zoneControl.ControlMode == NvApi.Gpu.Client.IlluminationControlMode.Manual ?
							new RgbwColor(zoneControl.Data.Rgbw.Manual.R, zoneControl.Data.Rgbw.Manual.G, zoneControl.Data.Rgbw.Manual.B, zoneControl.Data.Rgbw.Manual.W) :
							default
					)
				);
				break;
			case NvApi.Gpu.Client.IlluminationZoneType.SingleColor:
				lightingZones.Add
				(
					new SingleColorLightingZone
					(
						@lock,
						i,
						zoneId,
						zoneControl.ControlMode == NvApi.Gpu.Client.IlluminationControlMode.Manual ?
							zoneControl.Data.SingleColor.Manual.BrightnessPercentage :
							default
					)
				);
				break;
			default:
				logger.IlluminationZoneUnknownType((byte)i);
				continue;
			}
		}

		var fanStatuses = new NvApi.GpuFanStatus[32];
		var fanControls = new NvApi.GpuFanControl[32];
		int fanStatusCount = 0;
		int fanControlCount = 0;
		try
		{
			fanStatusCount = foundGpu.GetFanCoolersStatus(fanStatuses);
			fanControlCount = foundGpu.GetFanCoolersControl(fanControls);
		}
		catch
		{
		}

		bool hasTachReading = false;
		try
		{
			_ = foundGpu.GetTachReading();
			hasTachReading = true;
		}
		catch
		{
		}

		var thermalSensors = new NvApi.Gpu.ThermalSensor[3];
		int sensorCount = foundGpu.GetThermalSettings(thermalSensors);

		var clockFrequencies = new NvApi.GpuClockFrequency[32];
		int clockFrequencyCount = foundGpu.GetClockFrequencies(NvApi.Gpu.ClockType.Current, clockFrequencies);

		return new
		(
			new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new NVidiaGpuDriver
				(
					logger,
					loggerFactory.CreateLogger<UtilizationWatcher>(),
					deviceId,
					friendlyName,
					new("nv", topLevelDevice.Id, $"{NVidiaVendorId:X4}:{deviceId.ProductId:X4}", serialNumber.Length > 0 ? Convert.ToHexString(serialNumber) : null),
					foundGpu,
					@lock,
					zoneControls,
					lightingZones.DrainToImmutable(),
					hasTachReading,
					fanStatuses.AsSpan(0, fanStatusCount),
					fanControls.AsSpan(0, fanControlCount),
					thermalSensors.AsSpan(0, sensorCount),
					clockFrequencies.AsSpan(0, clockFrequencyCount)
				)
			)
		);
	}

	private readonly NvApi.Gpu.Client.IlluminationZoneControl[] _illuminationZoneControls;
	private readonly IDeviceFeatureSet<IDisplayAdapterDeviceFeature> _displayAdapterFeatures;
	private readonly IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<ICoolingDeviceFeature> _coolingFeatures;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly ImmutableArray<LightingZone> _lightingZones;
	private readonly ImmutableArray<ISensor> _sensors;
	private readonly NvApi.PhysicalGpu _gpu;
	private readonly Lock _lock;
	private readonly IReadOnlyCollection<ILightingZone> _lightingZoneCollection;
	private readonly ImmutableArray<FeatureSetDescription> _featureSets;
	private readonly UtilizationWatcher _utilizationWatcher;
	private readonly FanCoolerSensor?[] _fanCoolerSensors;
	private readonly ThermalTargetSensor[] _thermalTargetSensors;
	private readonly ClockSensor?[] _clockSensors;
	private int _fanCoolerGroupQueriedSensorCount;
	private int _thermalGroupQueriedSensorCount;
	private int _clockGroupQueriedSensorCount;
	private readonly ILogger<NVidiaGpuDriver> _logger;

	IReadOnlyCollection<ILightingZone> ILightingControllerFeature.LightingZones => _lightingZoneCollection;

	public DeviceId DeviceId { get; }
	public override DeviceCategory DeviceCategory => DeviceCategory.GraphicsAdapter;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IDisplayAdapterDeviceFeature> IDeviceDriver<IDisplayAdapterDeviceFeature>.Features => _displayAdapterFeatures;
	IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;
	IDeviceFeatureSet<ICoolingDeviceFeature> IDeviceDriver<ICoolingDeviceFeature>.Features => _coolingFeatures;

	public override ImmutableArray<FeatureSetDescription> FeatureSets => _featureSets;

	private NVidiaGpuDriver
	(
		ILogger<NVidiaGpuDriver> logger,
		ILogger<UtilizationWatcher> utilizationWatcherLogger,
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey,
		NvApi.PhysicalGpu gpu,
		Lock @lock,
		NvApi.Gpu.Client.IlluminationZoneControl[] zoneControls,
		ImmutableArray<LightingZone> lightingZones,
		bool hasTachReading,
		ReadOnlySpan<NvApi.GpuFanStatus> fanCoolerStatuses,
		ReadOnlySpan<NvApi.GpuFanControl> fanCoolerControls,
		ReadOnlySpan<NvApi.Gpu.ThermalSensor> thermalSensors,
		ReadOnlySpan<NvApi.GpuClockFrequency> clockFrequencies
	) : base(friendlyName, configurationKey)
	{
		_logger = logger;
		DeviceId = deviceId;
		_illuminationZoneControls = zoneControls;
		_lightingZones = lightingZones;
		_gpu = gpu;
		_lock = @lock;
		_utilizationWatcher = new(utilizationWatcherLogger, this);
		var sensors = ImmutableArray.CreateBuilder<ISensor>(3 + thermalSensors.Length + clockFrequencies.Length);
		sensors.Add(_utilizationWatcher.GraphicsSensor);
		sensors.Add(_utilizationWatcher.FrameBufferSensor);
		sensors.Add(_utilizationWatcher.VideoSensor);
		if (hasTachReading)
		{
			sensors.Add(new LegacyFanSensor(gpu));
		}
		// Nowadays, only the GPU thermal target would be returned. Usage of yet another undocumented API will be required. (To be done later)
		_thermalTargetSensors = new ThermalTargetSensor[thermalSensors.Length];
		for (int i = 0; i < thermalSensors.Length; i++)
		{
			sensors.Add(_thermalTargetSensors[i] = new(_gpu, thermalSensors[i], (byte)i));
		}
		// Process clocks while filtering out unsupported clocks.
		_clockSensors = new ClockSensor?[clockFrequencies.Length];
		for (int i = 0; i < clockFrequencies.Length; i++)
		{
			if (Enum.IsDefined(clockFrequencies[i].Clock))
			{
				sensors.Add(_clockSensors[i] = new ClockSensor(_gpu, clockFrequencies[i]));
			}
			else
			{
				_logger.GpuClockNotSupported(clockFrequencies[i].Clock);
			}
		}
		_fanCoolerSensors = new FanCoolerSensor?[fanCoolerStatuses.Length];
		for (int i = 0; i < fanCoolerStatuses.Length; i++)
		{
			var fanCoolerStatus = fanCoolerStatuses[i];
			// This API is not documented, so we don't really know all the details. Only fans with an ID that has been seen in the wild will be exposed as sensors. (Similar as for clocks above)
			if (fanCoolerStatus.FanId is 1 or 2)
			{
				sensors.Add(_fanCoolerSensors[i] = new(_gpu, fanCoolerStatus));
			}
		}
		_sensors = sensors.DrainToImmutable();
		FanCoolingControl? coolingControl = null;
		if (fanCoolerStatuses.Length == fanCoolerControls.Length)
		{
			coolingControl = new FanCoolingControl(gpu, fanCoolerControls);
		}
		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, NVidiaGpuDriver, IDeviceIdFeature>(this);
		_displayAdapterFeatures = FeatureSet.Create<IDisplayAdapterDeviceFeature, NVidiaGpuDriver, IDisplayAdapterI2cBusProviderFeature>(this);
		_lightingZoneCollection = ImmutableCollectionsMarshal.AsArray(lightingZones)!.AsReadOnly();
		_lightingFeatures = lightingZones.Length > 0 ?
			FeatureSet.Create<ILightingDeviceFeature, NVidiaGpuDriver, ILightingControllerFeature, ILightingDeferredChangesFeature>(this) :
			FeatureSet.Empty<ILightingDeviceFeature>();
		_sensorFeatures = _thermalTargetSensors.Length > 0 ?
			FeatureSet.Create<ISensorDeviceFeature, NVidiaGpuDriver, ISensorsFeature, ISensorsGroupedQueryFeature>(this) :
			FeatureSet.Create<ISensorDeviceFeature, NVidiaGpuDriver, ISensorsFeature>(this);
		_coolingFeatures = coolingControl is not null ?
			FeatureSet.Create<ICoolingDeviceFeature, FanCoolingControl, ICoolingControllerFeature>(coolingControl) :
			FeatureSet.Empty<ICoolingDeviceFeature>();

		var featureSets = new FeatureSetDescription[3 + (lightingZones.Length != 0 ? 1 : 0) + (coolingControl is not null ? 1 : 0)];
		int featureSetCount = 0;
		featureSets[featureSetCount++] = FeatureSetDescription.CreateStatic<IGenericDeviceFeature>();
		featureSets[featureSetCount++] = FeatureSetDescription.CreateStatic<IDisplayAdapterDeviceFeature>();
		if (lightingZones.Length != 0) featureSets[featureSetCount++] = FeatureSetDescription.CreateStatic<ILightingDeviceFeature>();
		featureSets[featureSetCount++] = FeatureSetDescription.CreateStatic<ISensorDeviceFeature>();
		if (coolingControl is not null) featureSets[featureSetCount++] = FeatureSetDescription.CreateStatic<ICoolingDeviceFeature>();

		_featureSets = ImmutableCollectionsMarshal.AsImmutableArray(featureSets);
	}

	public override ValueTask DisposeAsync() => _utilizationWatcher.DisposeAsync();

	LightingPersistenceMode ILightingDeferredChangesFeature.PersistenceMode => LightingPersistenceMode.NeverPersisted;

	ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync(bool shouldPersist)
	{
		try
		{
			lock (_lock)
			{
				foreach (var zone in _lightingZones)
				{
					zone.UpdateControl(_illuminationZoneControls);
				}
				_gpu.SetIlluminationZoneControls(_illuminationZoneControls, false);
			}
		}
		catch (Exception ex)
		{
			return ValueTask.FromException(ex);
		}
		return ValueTask.CompletedTask;
	}

	string IDisplayAdapterI2cBusProviderFeature.DeviceName => ConfigurationKey.DeviceMainId;

	ValueTask<II2cBus> IDisplayAdapterI2cBusProviderFeature.GetBusForMonitorAsync(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
	{
		var displays = _gpu.GetConnectedDisplays(default);
		foreach (var display in displays)
		{
			NvApi.System.GetGpuAndOutputIdFromDisplayId(display.DisplayId, out _, out uint outputId);

			byte[] rawEdid;
			Edid edid;
			try
			{
				rawEdid = _gpu.GetEdid(outputId);
			}
			catch (Exception ex)
			{
				_logger.EdidRetrievalFailure(display.DisplayId, outputId, FriendlyName, ex);
				continue;
			}
			try
			{
				edid = Edid.Parse(rawEdid);
			}
			catch (Exception ex)
			{
				_logger.EdidParsingFailure(display.DisplayId, outputId, FriendlyName, ex);
				continue;
			}
			if (edid.VendorId == vendorId && edid.ProductId == productId && edid.IdSerialNumber == idSerialNumber && edid.SerialNumber == serialNumber)
			{
				return new(new MonitorI2CBus(_gpu, outputId));
			}
		}
		return ValueTask.FromException<II2cBus>(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("Could not find the monitor.")));
	}

	void ISensorsGroupedQueryFeature.AddSensor(IPolledSensor sensor)
	{
		if (sensor is not GroupQueriedSensor s || s.Gpu != _gpu) throw new ArgumentException();
		if (!s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = true;
			if (s is FanCoolerSensor)
			{
				_fanCoolerGroupQueriedSensorCount++;
			}
			else if (s is ThermalTargetSensor)
			{
				_thermalGroupQueriedSensorCount++;
			}
			else if (s is ClockSensor)
			{
				_clockGroupQueriedSensorCount++;
			}
		}
	}

	void ISensorsGroupedQueryFeature.RemoveSensor(IPolledSensor sensor)
	{
		if (sensor is not GroupQueriedSensor s || s.Gpu != _gpu) throw new ArgumentException();
		if (s.IsGroupQueryEnabled)
		{
			s.IsGroupQueryEnabled = false;
			if (s is FanCoolerSensor)
			{
				_fanCoolerGroupQueriedSensorCount--;
			}
			else if (s is ThermalTargetSensor)
			{
				_thermalGroupQueriedSensorCount--;
			}
			else if (s is ClockSensor)
			{
				_clockGroupQueriedSensorCount--;
			}
		}
	}

	ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	{
		QueryFanCoolerSensors();
		QueryThermalSensors();
		QueryClockSensors();
		return ValueTask.CompletedTask;
	}

	private void QueryFanCoolerSensors()
	{
		if (_fanCoolerGroupQueriedSensorCount == 0) return;

		Span<NvApi.GpuFanStatus> fanStatuses = stackalloc NvApi.GpuFanStatus[32];

		try
		{
			int fanStatusCount = _gpu.GetFanCoolersStatus(fanStatuses);

			// The number of sensors is not supposed to change, and it will probably never happen, but we don't want to throw an exception here, as other sensor systems have to be queried.
			if (fanStatusCount == _fanCoolerSensors.Length)
			{
				for (int i = 0; i < fanStatusCount; i++)
				{
					_fanCoolerSensors[i]?.OnValueRead(fanStatuses[i].SpeedInRotationsPerMinute);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.GpuFanCoolerStatusQueryFailure(FriendlyName, ex);
		}
	}

	private void QueryThermalSensors()
	{
		if (_thermalGroupQueriedSensorCount == 0) return;

		Span<NvApi.Gpu.ThermalSensor> sensors = stackalloc NvApi.Gpu.ThermalSensor[3];

		try
		{
			int thermalSensorCount = _gpu.GetThermalSettings(sensors);

			// The number of sensors is not supposed to change, and it will probably never happen, but we don't want to throw an exception here, as other sensor systems have to be queried.
			if (thermalSensorCount == _thermalTargetSensors.Length)
			{
				for (int i = 0; i < thermalSensorCount; i++)
				{
					_thermalTargetSensors[i].OnValueRead((short)sensors[i].CurrentTemp);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.GpuClockFrequenciesQueryFailure(FriendlyName, ex);
		}
	}

	private void QueryClockSensors()
	{
		if (_clockGroupQueriedSensorCount == 0) return;

		Span<NvApi.GpuClockFrequency> clockFrequencies = stackalloc NvApi.GpuClockFrequency[32];

		try
		{
			int clockFrequencyCount = _gpu.GetClockFrequencies(NvApi.Gpu.ClockType.Current, clockFrequencies);

			// The number of sensors is not supposed to change, and it will probably never happen, but we don't want to throw an exception here, as other sensor systems have to be queried.
			if (clockFrequencyCount == _clockSensors.Length)
			{
				for (int i = 0; i < _clockSensors.Length; i++)
				{
					_clockSensors[i]?.OnValueRead(clockFrequencies[i].FrequencyInKiloHertz);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.GpuClockFrequenciesQueryFailure(FriendlyName, ex);
		}
	}

	private sealed class FanCoolingControl : ICoolingControllerFeature
	{
		private readonly uint[] _fanIds;
		private readonly sbyte[] _powers;
		private readonly NvApi.PhysicalGpu _physicalGpu;
		private bool _hasChanged;
		private readonly FanCooler[] _coolers;

		public FanCoolingControl(NvApi.PhysicalGpu physicalGpu, ReadOnlySpan<NvApi.GpuFanControl> fanControls)
		{
			var fanIds = new uint[fanControls.Length];
			var powers = new sbyte[fanControls.Length];
			var coolers = new FanCooler[fanControls.Length];

			// Keep track of how many know coolers have been registered. We will hide the unknown IDs for safety. (Only 1 and 2 seen at the time of writing this)
			int registeredCoolerCount = 0;
			for (int i = 0; i < fanControls.Length; i++)
			{
				ref readonly var fanControl = ref fanControls[i];

				fanIds[i] = fanControl.FanId;
				powers[i] = fanControl.CoolingMode == NvApi.FanCoolingMode.Manual ? (sbyte)fanControl.Power : (sbyte)-1;

				if (fanControl.FanId is 1)
				{
					coolers[registeredCoolerCount++] = new(this, i, Fan1CoolerId, Fan1SpeedSensorId);
				}
				else if (fanControl.FanId is 2)
				{
					coolers[registeredCoolerCount++] = new(this, i, Fan2CoolerId, Fan2SpeedSensorId);
				}
			}

			if (registeredCoolerCount < coolers.Length)
			{
				coolers = coolers[..registeredCoolerCount];
			}

			_physicalGpu = physicalGpu;
			_fanIds = fanIds;
			_powers = powers;
			_coolers = coolers;
		}

		ImmutableArray<ICooler> ICoolingControllerFeature.Coolers => ImmutableCollectionsMarshal.AsImmutableArray((ICooler[])_coolers);

		ValueTask ICoolingControllerFeature.ApplyChangesAsync(CancellationToken cancellationToken)
		{
			if (_hasChanged)
			{
				try
				{
					var fanIds = _fanIds;
					var powers = _powers;
					Span<NvApi.GpuFanControl> fanControls = stackalloc NvApi.GpuFanControl[fanIds.Length];

					for (int i = 0; i < fanControls.Length; i++)
					{
						uint fanId = fanIds[i];
						sbyte power = powers[i];
						fanControls[i] = new(fanId, power >= 0 ? (byte)power : (byte)0, power >= 0 ? NvApi.FanCoolingMode.Manual : NvApi.FanCoolingMode.Automatic);
					}

					_physicalGpu.SetFanCoolersControl(fanControls);
				}
				catch (Exception ex)
				{
					return ValueTask.FromException(ex);
				}
			}
			return ValueTask.CompletedTask;
		}

		private sealed class FanCooler : ICooler, IAutomaticCooler, IManualCooler
		{
			private readonly FanCoolingControl _control;
			private readonly int _index;
			private readonly Guid _coolerId;
			private readonly Guid _sensorId;

			public FanCooler(FanCoolingControl control, int index, Guid coolerId, Guid sensorId)
			{
				_control = control;
				_index = index;
				_coolerId = coolerId;
				_sensorId = sensorId;
			}

			private sbyte Power
			{
				get => _control._powers[_index];
				set
				{
					ref var power = ref _control._powers[_index];
					if (value != power)
					{
						Volatile.Write(ref power, value);
						_control._hasChanged = true;
					}
				}
			}

			Guid ICooler.CoolerId => _coolerId;
			Guid? ICooler.SpeedSensorId => _sensorId;
			CoolerType ICooler.Type => CoolerType.Fan;
			CoolingMode ICooler.CoolingMode => Power < 0 ? CoolingMode.Automatic : CoolingMode.Manual;

			void IAutomaticCooler.SwitchToAutomaticCooling() => Power = -1;

			void IManualCooler.SetPower(byte power)
			{
				ArgumentOutOfRangeException.ThrowIfGreaterThan(power, 100);
				Power = (sbyte)power;
			}

			bool IManualCooler.TryGetPower(out byte power)
			{
				var p = Power;
				if ((byte)p <= 100)
				{
					power = (byte)p;
					return true;
				}
				else
				{
					power = 0;
					return false;
				}
			}

			byte IConfigurableCooler.MinimumPower => 30;
			bool IConfigurableCooler.CanSwitchOff => false;
		}
	}
}
