using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.DisplayDevices;
using Exo.ColorFormats;
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

		var fanInfos = new NvApi.GpuFanInfo[32];
		var fanStatuses = new NvApi.GpuFanStatus[32];
		var fanControls = new NvApi.GpuFanControl[32];
		int fanInfoCount = 0;
		int fanStatusCount = 0;
		int fanControlCount = 0;
		try
		{
			fanInfoCount = foundGpu.GetFanCoolersInfo(fanInfos);
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
		int clockFrequencyCount = 0;
		try
		{
			clockFrequencyCount = foundGpu.GetClockFrequencies(NvApi.Gpu.ClockType.Current, clockFrequencies);
		}
		catch (NvApiException ex) when (ex.Status == NvApiError.GpuNotPowered)
		{
			// TODO: Log a warning.
			// We can still retrieve the base frequencies if the GPU is not powered.
			// For all intents and purposes, we will consider the frequencies to be zero.
			clockFrequencyCount = foundGpu.GetClockFrequencies(NvApi.Gpu.ClockType.Base, clockFrequencies);
			for (int i = 0; i < clockFrequencyCount; i++)
			{
				clockFrequencies[i] = new(clockFrequencies[i].Clock, 0);
			}
		}

		var dynamicPStateInfos = new NvApi.GpuDynamicPStateInfo[8];
		int dynamicPStateCount = 0;
		try
		{
			dynamicPStateCount = foundGpu.GetDynamicPStatesInfoEx(dynamicPStateInfos);
		}
		catch (NvApiException ex) when (ex.Status == NvApiError.GpuNotPowered)
		{
			// TODO: Log a warning.
			// When the API returns NVAPI_GPU_NOT_POWERED, assume that all known sensors are available.
			// This is not perfect, but it is better than nothing.
			dynamicPStateInfos[0] = new(NvApi.Gpu.Client.UtilizationDomain.Graphics, 0);
			dynamicPStateInfos[1] = new(NvApi.Gpu.Client.UtilizationDomain.FrameBuffer, 0);
			dynamicPStateInfos[2] = new(NvApi.Gpu.Client.UtilizationDomain.Video, 0);
			dynamicPStateInfos[3] = new(NvApi.Gpu.Client.UtilizationDomain.Bus, 0);
			dynamicPStateCount = 4;
		}
		catch
		{
		}

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
					fanInfos.AsSpan(0, fanInfoCount),
					fanStatuses.AsSpan(0, fanStatusCount),
					fanControls.AsSpan(0, fanControlCount),
					thermalSensors.AsSpan(0, sensorCount),
					clockFrequencies.AsSpan(0, clockFrequencyCount),
					dynamicPStateInfos.AsSpan(0, dynamicPStateCount)
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
	private readonly UtilizationWatcher? _utilizationWatcher;
	private readonly DynamicPStateSensor[] _dynamicPStateSensors;
	private readonly FanCoolerSensor[] _fanCoolerSensors;
	private readonly ThermalTargetSensor[] _thermalTargetSensors;
	private readonly ClockSensor?[] _clockSensors;
	private int _fanCoolerGroupQueriedSensorCount;
	private int _thermalGroupQueriedSensorCount;
	private int _clockGroupQueriedSensorCount;
	private int _dynamicPStateGroupQueriedSensorCount;
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
		ReadOnlySpan<NvApi.GpuFanInfo> fanCoolerInfos,
		ReadOnlySpan<NvApi.GpuFanStatus> fanCoolerStatuses,
		ReadOnlySpan<NvApi.GpuFanControl> fanCoolerControls,
		ReadOnlySpan<NvApi.Gpu.ThermalSensor> thermalSensors,
		ReadOnlySpan<NvApi.GpuClockFrequency> clockFrequencies,
		ReadOnlySpan<NvApi.GpuDynamicPStateInfo> dynamicPStateInfos
	) : base(friendlyName, configurationKey)
	{
		_logger = logger;
		DeviceId = deviceId;
		_illuminationZoneControls = zoneControls;
		_lightingZones = lightingZones;
		_gpu = gpu;
		_lock = @lock;
		var sensors = ImmutableArray.CreateBuilder<ISensor>(4 + fanCoolerInfos.Length + thermalSensors.Length + clockFrequencies.Length);
		if (dynamicPStateInfos.Length > 0)
		{
			var pStateSensors = new DynamicPStateSensor[dynamicPStateInfos.Length];
			int count = 0;
			for (int i = 0; i < dynamicPStateInfos.Length; i++)
			{
				ref readonly var info = ref dynamicPStateInfos[i];
				if (info.Domain <= NvApi.Gpu.Client.UtilizationDomain.Bus)
				{
					sensors.Add(pStateSensors[count++] = new(gpu, info.Domain, info.Percent));
				}
			}
			_dynamicPStateSensors = count < pStateSensors.Length ? pStateSensors[..count] : pStateSensors;
		}
		else
		{
			_dynamicPStateSensors = [];
			_utilizationWatcher = new(utilizationWatcherLogger, this);
			sensors.Add(_utilizationWatcher.GraphicsSensor);
			sensors.Add(_utilizationWatcher.FrameBufferSensor);
			sensors.Add(_utilizationWatcher.VideoSensor);
		}
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
		FanCoolingControl? coolingControl = null;
		if (fanCoolerInfos.Length == fanCoolerStatuses.Length)
		{
			var fanCoolerSensors = new FanCoolerSensor[fanCoolerStatuses.Length];
			int count = 0;
			for (int i = 0; i < fanCoolerStatuses.Length; i++)
			{
				ref readonly var fanCoolerInfo = ref fanCoolerInfos[i];
				ref readonly var fanCoolerStatus = ref fanCoolerStatuses[i];
				// This API is not documented, so we don't really know all the details. Only fans with an ID that has been seen in the wild will be exposed as sensors. (Similar as for clocks above)
				// TODO: Extend the fan ID support.
				if (fanCoolerStatus.FanId is 1 or 2)
				{
					sensors.Add(fanCoolerSensors[count++] = new(_gpu, in fanCoolerInfo, in fanCoolerStatus));
				}
			}
			_fanCoolerSensors = count < fanCoolerSensors.Length ? fanCoolerSensors[..count] : fanCoolerSensors;
			if (fanCoolerStatuses.Length == fanCoolerControls.Length)
			{
				coolingControl = new FanCoolingControl(gpu, fanCoolerInfos, fanCoolerStatuses, fanCoolerControls);
			}
		}
		else
		{
			_fanCoolerSensors = [];
		}
		_sensors = sensors.DrainToImmutable();
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

	public override ValueTask DisposeAsync()
	{
		if (_utilizationWatcher is { } utilizationWatcher)
		{
			return utilizationWatcher.DisposeAsync();
		}
		else
		{
			return ValueTask.CompletedTask;
		}
	}

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
			else if (s is DynamicPStateSensor)
			{
				_dynamicPStateGroupQueriedSensorCount++;
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
			else if (s is DynamicPStateSensor)
			{
				_dynamicPStateGroupQueriedSensorCount--;
			}
		}
	}

	ValueTask ISensorsGroupedQueryFeature.QueryValuesAsync(CancellationToken cancellationToken)
	{
		QueryFanCoolerSensors();
		QueryThermalSensors();
		QueryClockSensors();
		QueryDynamicPStateSensors();
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

			var thermalSensors = _thermalTargetSensors;
			// The number of sensors is not supposed to change, and it will probably never happen, but we don't want to throw an exception here, as other sensor systems have to be queried.
			if (thermalSensorCount == thermalSensors.Length)
			{
				for (int i = 0; i < thermalSensorCount; i++)
				{
					thermalSensors[i].OnValueRead((short)sensors[i].CurrentTemp);
				}
			}
		}
		catch (NvApiException ex) when (ex.Status == NvApiError.GpuNotPowered)
		{
			var thermalSensors = _thermalTargetSensors;
			for (int i = 0; i < thermalSensors.Length; i++)
			{
				thermalSensors[i]?.OnValueRead(0);
			}
		}
		catch (Exception ex)
		{
			_logger.GpuThermalSettingsQueryFailure(FriendlyName, ex);
		}
	}

	private void QueryClockSensors()
	{
		if (_clockGroupQueriedSensorCount == 0) return;

		Span<NvApi.GpuClockFrequency> clockFrequencies = stackalloc NvApi.GpuClockFrequency[32];

		try
		{
			int clockFrequencyCount = _gpu.GetClockFrequencies(NvApi.Gpu.ClockType.Current, clockFrequencies);

			var sensors = _clockSensors;
			// The number of sensors is not supposed to change, and it will probably never happen, but we don't want to throw an exception here, as other sensor systems have to be queried.
			if (clockFrequencyCount == sensors.Length)
			{
				for (int i = 0; i < sensors.Length; i++)
				{
					sensors[i]?.OnValueRead(clockFrequencies[i].FrequencyInKiloHertz);
				}
			}
		}
		catch (NvApiException ex) when (ex.Status == NvApiError.GpuNotPowered)
		{
			var sensors = _clockSensors;
			for (int i = 0; i < sensors.Length; i++)
			{
				sensors[i]?.OnValueRead(0);
			}
		}
		catch (Exception ex)
		{
			_logger.GpuClockFrequenciesQueryFailure(FriendlyName, ex);
		}
	}

	private void QueryDynamicPStateSensors()
	{
		if (_dynamicPStateGroupQueriedSensorCount == 0) return;

		Span<NvApi.GpuDynamicPStateInfo> dynamicPStateInfos = stackalloc NvApi.GpuDynamicPStateInfo[8];

		try
		{
			int dynamicPStateInfoCount = _gpu.GetDynamicPStatesInfoEx(dynamicPStateInfos);

			var sensors = _dynamicPStateSensors;
			// The number of sensors is not supposed to change, and it will probably never happen, but we don't want to throw an exception here, as other sensor systems have to be queried.
			if (dynamicPStateInfoCount == sensors.Length)
			{
				for (int i = 0; i < sensors.Length; i++)
				{
					sensors[i]?.OnValueRead(dynamicPStateInfos[i].Percent);
				}
			}
		}
		catch (NvApiException ex) when (ex.Status == NvApiError.GpuNotPowered)
		{
			var sensors = _dynamicPStateSensors;
			for (int i = 0; i < sensors.Length; i++)
			{
				sensors[i]?.OnValueRead(0);
			}
		}
		catch (Exception ex)
		{
			_logger.GpuDynamicPStatesQueryFailure(FriendlyName, ex);
		}
	}
}
