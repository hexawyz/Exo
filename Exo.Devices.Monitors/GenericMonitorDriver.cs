using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.MonitorFeatures;
using Exo.I2C;

namespace Exo.Devices.Monitors;

public class GenericMonitorDriver
	: Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	IMonitorCapabilitiesFeature,
	IMonitorRawCapabilitiesFeature,
	IMonitorBrightnessFeature,
	IMonitorContrastFeature,
	IMonitorSpeakerAudioVolumeFeature
{
	[DiscoverySubsystem<MonitorDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Monitor)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		DeviceId deviceId,
		Edid edid,
		II2CBus i2cBus,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		var features = SupportedFeatures.None;

		var ddc = new DisplayDataChannel(i2cBus, true);

		var buffer = ArrayPool<byte>.Shared.Rent(1000);
		byte[] rawCapabilities;
		try
		{
			ushort length = await ddc.GetCapabilitiesAsync(buffer, cancellationToken).ConfigureAwait(false);
			rawCapabilities = buffer[..length].ToArray();
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}

		if (MonitorCapabilities.TryParse(rawCapabilities, out var capabilities))
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

		return new DriverCreationResult<SystemDevicePath>
		(
			keys,
			new GenericMonitorDriver
			(
				ddc,
				features,
				rawCapabilities,
				capabilities,
				deviceId,
				friendlyName,
				new("monitor", topLevelDeviceName, deviceId.ToString(), edid.SerialNumber)
			)
		);
	}

	[Flags]
	protected enum SupportedFeatures : ulong
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

		public bool IsEmpty => _driver._supportedFeatures == SupportedFeatures.None;

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

	private readonly DisplayDataChannel _ddc;
	private readonly SupportedFeatures _supportedFeatures;
	private readonly ReadOnlyMemory<byte> _rawCapabilities;
	private readonly MonitorCapabilities? _capabilities;
	private readonly DeviceId _deviceId;

	private readonly IDeviceFeatureCollection<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;

	IDeviceFeatureCollection<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureCollection<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	DeviceId IDeviceIdFeature.DeviceId => _deviceId;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	protected GenericMonitorDriver
	(
		DisplayDataChannel ddc,
		SupportedFeatures supportedFeatures,
		ReadOnlyMemory<byte> rawCapabilities,
		MonitorCapabilities? capabilities,
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	)
		: base(friendlyName, configurationKey)
	{
		_ddc = ddc;
		_supportedFeatures = supportedFeatures;
		_rawCapabilities = rawCapabilities;
		_capabilities = capabilities;
		_deviceId = deviceId;

		_genericFeatures = configurationKey.UniqueId is not null ?
			FeatureCollection.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureCollection.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature>(this);

		_monitorFeatures = new MonitorFeatureCollection(this);
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private void EnsureSupportedFeatures(SupportedFeatures features)
	{
		if ((_supportedFeatures & features) != features) throw new NotSupportedException();
	}

	private async ValueTask<ContinuousValue> GetVcpAsync(SupportedFeatures features, VcpCode code, CancellationToken cancellationToken)
	{
		EnsureSupportedFeatures(features);
		var reply = await _ddc.GetVcpFeatureAsync((byte)code, cancellationToken).ConfigureAwait(false);
		return new ContinuousValue(reply.CurrentValue, 0, reply.MaximumValue);
	}

	private async ValueTask SetVcpAsync(SupportedFeatures features, VcpCode code, ushort value, CancellationToken cancellationToken)
	{
		EnsureSupportedFeatures(features);
		await _ddc.SetVcpFeatureAsync((byte)code, value, cancellationToken).ConfigureAwait(false);
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
