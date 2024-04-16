using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using DeviceTools;
using DeviceTools.DisplayDevices;
using Exo.ColorFormats;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.LightingFeatures;
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
	IDisplayAdapterI2CBusProviderFeature,
	ILightingControllerFeature,
	ILightingDeferredChangesFeature,
	ISensorsFeature
{
	private sealed class UtilizationWatcher : IAsyncDisposable
	{
		private readonly NvApi.PhysicalGpu _gpu;
		private readonly UtilizationSensor _graphicsSensor;
		private readonly UtilizationSensor _frameBufferSensor;
		private readonly UtilizationSensor _videoSensor;
		private int _referenceCount;
		private readonly object _lock;
		private TaskCompletionSource _enableSignal;
		private CancellationTokenSource? _disableCancellationTokenSource;
		private CancellationTokenSource? _disposeCancellationTokenSource;
		private readonly Task _runTask;

		public UtilizationSensor GraphicsSensor => _graphicsSensor;
		public UtilizationSensor FrameBufferSensor => _frameBufferSensor;
		public UtilizationSensor VideoSensor => _videoSensor;

		public UtilizationWatcher(NvApi.PhysicalGpu gpu)
		{
			_gpu = gpu;
			_graphicsSensor = new(this, GraphicsUtilizationSensorId);
			_frameBufferSensor = new(this, FrameBufferUtilizationSensorId);
			_videoSensor = new(this, VideoUtilizationSensorId);
			_lock = new();
			_enableSignal = new();
			_disposeCancellationTokenSource = new();
			_runTask = RunAsync(_disposeCancellationTokenSource.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposeCancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				Volatile.Read(ref _enableSignal).TrySetResult();
				await _runTask.ConfigureAwait(false);
				cts.Dispose();
			}
		}

		public void Acquire()
		{
			lock (_lock)
			{
				if (_referenceCount++ == 0)
				{
					_enableSignal.TrySetResult();
				}
			}
		}

		// This function is called by a sensor state to cancel grouped querying for it.
		// NB: The sensor state *WILL* ensure that this method is never called twice in succession for a given sensor.
		public void Release()
		{
			lock (_lock)
			{
				if (--_referenceCount == 0)
				{
					if (Interlocked.Exchange(ref _disableCancellationTokenSource, null) is { } cts)
					{
						cts.Cancel();
						cts.Dispose();
					}
				}
			}
		}

		private async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					await _enableSignal.Task.ConfigureAwait(false);
					if (cancellationToken.IsCancellationRequested) return;
					var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					var queryCancellationToken = cts.Token;
					Volatile.Write(ref _disableCancellationTokenSource, cts);
					try
					{
						await WatchValuesAsync(queryCancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (queryCancellationToken.IsCancellationRequested)
					{
					}
					if (cancellationToken.IsCancellationRequested) return;
					cts = Interlocked.Exchange(ref _disableCancellationTokenSource, null);
					cts?.Dispose();
					Volatile.Write(ref _enableSignal, new());
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				// TODO: Log
			}
		}

		private async ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			await foreach (var utilizationValue in _gpu.WatchUtilizationAsync(500, cancellationToken).ConfigureAwait(false))
			{
				var sensor = utilizationValue.Domain switch
				{
					NvApi.Gpu.Client.UtilizationDomain.Graphics => _graphicsSensor,
					NvApi.Gpu.Client.UtilizationDomain.FrameBuffer => _frameBufferSensor,
					NvApi.Gpu.Client.UtilizationDomain.Video => _videoSensor,
					_ => null,
				};
				sensor?.OnDataReceived(utilizationValue.DateTime, utilizationValue.PerTenThousandValue);
			}
		}
	}

	private sealed class UtilizationSensor : IStreamedSensor<float>
	{
		private ChannelWriter<SensorDataPoint<float>>? _listener;
		private readonly UtilizationWatcher _watcher;

		public Guid SensorId { get; }

		public UtilizationSensor(UtilizationWatcher watcher, Guid sensorId)
		{
			_watcher = watcher;
			SensorId = sensorId;
		}

		public float? ScaleMinimumValue => 0;
		public float? ScaleMaximumValue => 100;
		public SensorUnit Unit => SensorUnit.Percent;

		public async IAsyncEnumerable<SensorDataPoint<float>> EnumerateValuesAsync(CancellationToken cancellationToken)
		{
			var channel = Channel.CreateUnbounded<SensorDataPoint<float>>(SharedOptions.ChannelOptions);
			if (Interlocked.CompareExchange(ref _listener, channel, null) is not null) throw new InvalidOperationException("An enumeration is already running.");
			try
			{
				_watcher.Acquire();
				try
				{
					await foreach (var dataPoint in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
					{
						yield return dataPoint;
					}
				}
				finally
				{
					_watcher.Release();
				}
			}
			finally
			{
				Volatile.Write(ref _listener, null);
			}
		}

		public void OnDataReceived(DateTime dateTime, uint value) => Volatile.Read(ref _listener)?.TryWrite(new SensorDataPoint<float>(dateTime, value * 0.01f));
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

	private static readonly Guid GraphicsUtilizationSensorId = new(0x005F94DD, 0x09F5, 0x46D3, 0x99, 0x02, 0xE1, 0x5D, 0x6A, 0x19, 0xD8, 0x24);
	private static readonly Guid FrameBufferUtilizationSensorId = new(0xBF9AAD1D, 0xE013, 0x4178, 0x97, 0xB3, 0x42, 0x20, 0xD2, 0x6C, 0xBE, 0x71);
	private static readonly Guid VideoUtilizationSensorId = new(0x147C8F52, 0x1402, 0x4515, 0xB9, 0xFB, 0x41, 0x48, 0xFD, 0x02, 0x12, 0xA4);

	[DiscoverySubsystem<PciDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayAdapter)]
	[DeviceInterfaceClass(DeviceInterfaceClass.DisplayDeviceArrival)]
	[VendorId(VendorIdSource.Pci, NVidiaVendorId)]
	public static ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		DeviceObjectInformation topLevelDevice,
		DeviceId deviceId,
		ILogger<NVidiaGpuDriver> logger
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

		var @lock = new object();

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

		var thermalSensors = new NvApi.Gpu.ThermalSensor[3];
		int sensorCount = foundGpu.GetThermalSettings(thermalSensors);

		return new
		(
			new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new NVidiaGpuDriver
				(
					deviceId,
					friendlyName,
					new("nv", topLevelDevice.Id, $"{NVidiaVendorId:X4}:{deviceId.ProductId:X4}", serialNumber.Length > 0 ? Convert.ToHexString(serialNumber) : null),
					foundGpu,
					@lock,
					zoneControls,
					lightingZones.DrainToImmutable()
				)
			)
		);
	}

	private readonly NvApi.Gpu.Client.IlluminationZoneControl[] _illuminationZoneControls;
	private readonly IDeviceFeatureSet<IDisplayAdapterDeviceFeature> _displayAdapterFeatures;
	private readonly IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureSet<ISensorDeviceFeature> _sensorFeatures;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly ImmutableArray<LightingZone> _lightingZones;
	private readonly ImmutableArray<ISensor> _sensors;
	private readonly NvApi.PhysicalGpu _gpu;
	private readonly object _lock;
	private readonly IReadOnlyCollection<ILightingZone> _lightingZoneCollection;
	private readonly ImmutableArray<FeatureSetDescription> _featureSets;
	private readonly UtilizationWatcher _utilizationWatcher;

	IReadOnlyCollection<ILightingZone> ILightingControllerFeature.LightingZones => _lightingZoneCollection;

	public DeviceId DeviceId { get; }
	public override DeviceCategory DeviceCategory => DeviceCategory.GraphicsAdapter;

	ImmutableArray<ISensor> ISensorsFeature.Sensors => _sensors;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IDisplayAdapterDeviceFeature> IDeviceDriver<IDisplayAdapterDeviceFeature>.Features => _displayAdapterFeatures;
	IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	IDeviceFeatureSet<ISensorDeviceFeature> IDeviceDriver<ISensorDeviceFeature>.Features => _sensorFeatures;

	public override ImmutableArray<FeatureSetDescription> FeatureSets => _featureSets;

	private NVidiaGpuDriver
	(
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey,
		NvApi.PhysicalGpu gpu,
		object @lock,
		NvApi.Gpu.Client.IlluminationZoneControl[] zoneControls,
		ImmutableArray<LightingZone> lightingZones
	) : base(friendlyName, configurationKey)
	{
		DeviceId = deviceId;
		_illuminationZoneControls = zoneControls;
		_lightingZones = lightingZones;
		_gpu = gpu;
		_lock = @lock;
		_utilizationWatcher = new(gpu);
		_sensors = [_utilizationWatcher.GraphicsSensor, _utilizationWatcher.FrameBufferSensor, _utilizationWatcher.VideoSensor];
		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, NVidiaGpuDriver, IDeviceIdFeature>(this);
		_displayAdapterFeatures = FeatureSet.Create<IDisplayAdapterDeviceFeature, NVidiaGpuDriver, IDisplayAdapterI2CBusProviderFeature>(this);
		_lightingZoneCollection = ImmutableCollectionsMarshal.AsArray(lightingZones)!.AsReadOnly();
		_lightingFeatures = lightingZones.Length > 0 ?
			FeatureSet.Create<ILightingDeviceFeature, NVidiaGpuDriver, ILightingControllerFeature, ILightingDeferredChangesFeature>(this) :
			FeatureSet.Empty<ILightingDeviceFeature>();
		_sensorFeatures = FeatureSet.Create<ISensorDeviceFeature, NVidiaGpuDriver, ISensorsFeature>(this);
		_featureSets = lightingZones.Length > 0 ?
			[
				FeatureSetDescription.CreateStatic<IGenericDeviceFeature>(),
				FeatureSetDescription.CreateStatic<IDisplayAdapterDeviceFeature>(),
				FeatureSetDescription.CreateStatic<ISensorDeviceFeature>()
			] :
			[
				FeatureSetDescription.CreateStatic<IGenericDeviceFeature>(),
				FeatureSetDescription.CreateStatic<IDisplayAdapterDeviceFeature>(),
				FeatureSetDescription.CreateStatic<ILightingDeviceFeature>(),
				FeatureSetDescription.CreateStatic<ISensorDeviceFeature>()
			];
	}

	public override ValueTask DisposeAsync() => _utilizationWatcher.DisposeAsync();

	public ValueTask ApplyChangesAsync()
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

	string IDisplayAdapterI2CBusProviderFeature.DeviceName => ConfigurationKey.DeviceMainId;

	ValueTask<II2CBus> IDisplayAdapterI2CBusProviderFeature.GetBusForMonitorAsync(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
	{
		var displays = _gpu.GetConnectedDisplays(default);
		foreach (var display in displays)
		{
			NvApi.System.GetGpuAndOutputIdFromDisplayId(display.DisplayId, out _, out uint outputId);
			try
			{
				var edid = Edid.Parse(_gpu.GetEdid(outputId));
				if (edid.VendorId == vendorId && edid.ProductId == productId && edid.IdSerialNumber == idSerialNumber && edid.SerialNumber == serialNumber)
				{
					return new(new MonitorI2CBus(_gpu, outputId));
				}
			}
			catch (Exception ex)
			{
				// TODO: Log.
			}
		}
		return ValueTask.FromException<II2CBus>(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("Could not find the monitor.")));
	}
}
