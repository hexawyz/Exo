using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.MonitorFeatures;

namespace Exo.Devices.Monitors;

public class GenericMonitorDriver
	: Driver,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceIdFeature,
	IMonitorCapabilitiesFeature,
	IMonitorRawCapabilitiesFeature,
	IMonitorBrightnessFeature,
	IMonitorContrastFeature,
	IMonitorSpeakerAudioVolumeFeature
{
	[DiscoverySubsystem<MonitorDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Monitor)]
	public static ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		DeviceId deviceId,
		PhysicalMonitor physicalMonitor,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		var features = SupportedFeatures.None;
		var rawCapabilities = physicalMonitor.GetCapabilitiesUtf8String();
		if (MonitorCapabilities.TryParse(rawCapabilities.Span, out var capabilities))
		{
			features |= SupportedFeatures.Capabilities;

			foreach (var capability in capabilities.SupportedVcpCommands)
			{
				if (capability.VcpCode == (byte)VcpCode.Luminance)
				{
					features |= SupportedFeatures.Brightness;
				}
				else if (capability.VcpCode == (byte)VcpCode.Contrast)
				{
					features |= SupportedFeatures.Contrast;
				}
				else if (capability.VcpCode == (byte)VcpCode.AudioSpeakerVolume)
				{
					features |= SupportedFeatures.AudioVolume;
				}
			}
		}
		return new
		(
			new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new GenericMonitorDriver
				(
					physicalMonitor,
					features,
					rawCapabilities,
					capabilities,
					deviceId,
					friendlyName,
					new("monitor", topLevelDeviceName, deviceId.ToString(), null)
				)
			)
		);
	}

	[Flags]
	private enum SupportedFeatures : ulong
	{
		None = 0x00000000,
		Capabilities = 0x00000001,
		Brightness = 0x00000002,
		Contrast = 0x00000004,
		AudioVolume = 0x00000008,
	}

	private sealed class MonitorFeatureCollection : IDeviceFeatureCollection<IMonitorDeviceFeature>
	{
		private readonly GenericMonitorDriver _driver;
		private Dictionary<Type, IMonitorDeviceFeature>? _cachedFeatureDictionary;

		public MonitorFeatureCollection(GenericMonitorDriver driver) => _driver = driver;

		IMonitorDeviceFeature? IDeviceFeatureCollection<IMonitorDeviceFeature>.this[Type type]
			=> (_cachedFeatureDictionary ??= new(this))[type];

		T? IDeviceFeatureCollection<IMonitorDeviceFeature>.GetFeature<T>() where T : class
		{
			var supportedFeatures = _driver._supportedFeatures;
			if (typeof(T) == typeof(IMonitorCapabilitiesFeature) && (supportedFeatures & SupportedFeatures.Capabilities) != 0 ||
				typeof(T) == typeof(IMonitorRawCapabilitiesFeature) && (supportedFeatures & SupportedFeatures.Capabilities) != 0 ||
				typeof(T) == typeof(IMonitorBrightnessFeature) && (supportedFeatures & SupportedFeatures.Brightness) != 0 ||
				typeof(T) == typeof(IMonitorContrastFeature) && (supportedFeatures & SupportedFeatures.Contrast) != 0 ||
				typeof(T) == typeof(IMonitorSpeakerAudioVolumeFeature) && (supportedFeatures & SupportedFeatures.AudioVolume) != 0)
			{
				return Unsafe.As<T>(_driver);
			}
			return null;
		}

		IEnumerator<KeyValuePair<Type, IMonitorDeviceFeature>> IEnumerable<KeyValuePair<Type, IMonitorDeviceFeature>>.GetEnumerator()
		{
			var supportedFeatures = _driver._supportedFeatures;
			if ((supportedFeatures & SupportedFeatures.Capabilities) != 0)
			{
				yield return new(typeof(IMonitorCapabilitiesFeature), _driver);
				yield return new(typeof(IMonitorRawCapabilitiesFeature), _driver);
			}
			if ((supportedFeatures & SupportedFeatures.Brightness) != 0) yield return new(typeof(IMonitorBrightnessFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.Contrast) != 0) yield return new(typeof(IMonitorContrastFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.AudioVolume) != 0) yield return new(typeof(IMonitorSpeakerAudioVolumeFeature), _driver);
		}

		IEnumerator IEnumerable.GetEnumerator() => Unsafe.As<IEnumerable<KeyValuePair<Type, IMonitorDeviceFeature>>>(this).GetEnumerator();
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private readonly PhysicalMonitor _physicalMonitor;
	private readonly SupportedFeatures _supportedFeatures;
	private readonly ReadOnlyMemory<byte> _rawCapabilities;
	private readonly MonitorCapabilities? _capabilities;
	private readonly DeviceId _deviceId;

	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features { get; }
	IDeviceFeatureCollection<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	DeviceId IDeviceIdFeature.DeviceId => _deviceId;

	private GenericMonitorDriver
	(
		PhysicalMonitor physicalMonitor,
		SupportedFeatures supportedFeatures,
		ReadOnlyMemory<byte> rawCapabilities,
		MonitorCapabilities? capabilities,
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	)
		: base(friendlyName, configurationKey)
	{
		_physicalMonitor = physicalMonitor;
		_supportedFeatures = supportedFeatures;
		_rawCapabilities = rawCapabilities;
		_capabilities = capabilities;
		_deviceId = deviceId;
		_monitorFeatures = new MonitorFeatureCollection(this);

		Features = FeatureCollection.CreateMerged(_monitorFeatures, FeatureCollection.Create<IDeviceFeature, GenericMonitorDriver, IDeviceIdFeature>(this));
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private void EnsureSupportedFeatures(SupportedFeatures features)
	{
		if ((_supportedFeatures & features) != features) throw new NotSupportedException();
	}

	private ValueTask<ContinuousValue> GetVcpAsync(SupportedFeatures features, VcpCode code, CancellationToken cancellationToken)
	{
		try
		{
			EnsureSupportedFeatures(features);
			var reply = _physicalMonitor.GetVcpFeature((byte)code);
			return ValueTask.FromResult(new ContinuousValue(reply.CurrentValue, 0, reply.MaximumValue));
		}
		catch (Exception ex)
		{
			return ValueTask.FromException<ContinuousValue>(ex);
		}
	}

	private ValueTask SetVcpAsync(SupportedFeatures features, VcpCode code, ushort value, CancellationToken cancellationToken)
	{
		try
		{
			EnsureSupportedFeatures(features);
			_physicalMonitor.SetVcpFeature((byte)code, value);
			return ValueTask.CompletedTask;
		}
		catch (Exception ex)
		{
			return ValueTask.FromException(ex);
		}
	}

	ReadOnlySpan<byte> IMonitorRawCapabilitiesFeature.RawCapabilities
	{
		get
		{
			EnsureSupportedFeatures(SupportedFeatures.Capabilities);
			return _rawCapabilities.Span;
		}
	}

	MonitorCapabilities IMonitorCapabilitiesFeature.Capabilities
	{
		get
		{
			EnsureSupportedFeatures(SupportedFeatures.Capabilities);
			return _capabilities!;
		}
	}

	ValueTask<ContinuousValue> IMonitorBrightnessFeature.GetBrightnessAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.Brightness, VcpCode.Luminance, cancellationToken);
	ValueTask IMonitorBrightnessFeature.SetBrightnessAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.Brightness, VcpCode.Luminance, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorContrastFeature.GetContrastAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.Contrast, VcpCode.Contrast, cancellationToken);
	ValueTask IMonitorContrastFeature.SetContrastAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.Contrast, VcpCode.Contrast, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorSpeakerAudioVolumeFeature.GetVolumeAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.Contrast, VcpCode.AudioSpeakerVolume, cancellationToken);
	ValueTask IMonitorSpeakerAudioVolumeFeature.SetVolumeAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.Contrast, VcpCode.AudioSpeakerVolume, value, cancellationToken);
}
