using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Monitors;
using Exo.I2C;
using Exo.Metadata;
using Exo.Monitors;
using Microsoft.Extensions.Logging;

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
	IMonitorSpeakerAudioVolumeFeature,
	IMonitorInputSelectFeature
{
	private static readonly ExoArchive MonitorDefinitionsDatabase = new((UnmanagedMemoryStream)typeof(GenericMonitorDriver).Assembly.GetManifestResourceStream("Definitions.xoa")!);

	private static bool TryGetMonitorDefinition(DeviceId deviceId, out MonitorDefinition definition)
	{
		Span<byte> key = stackalloc byte[4];
		BinaryPrimitives.WriteUInt16LittleEndian(key, deviceId.VendorId);
		BinaryPrimitives.WriteUInt16LittleEndian(key[2..], deviceId.ProductId);
		if (MonitorDefinitionsDatabase.TryGetFileEntry(key, out var file))
		{
			definition = MonitorDefinitionSerializer.Deserialize(file.DangerousGetSpan());
			return true;
		}
		else
		{
			definition = default;
			return false;
		}
	}

	[DiscoverySubsystem<MonitorDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Monitor)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<GenericMonitorDriver> logger,
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
		ImmutableArray<byte> rawCapabilities;
		try
		{
			ushort length = await ddc.GetCapabilitiesAsync(buffer, cancellationToken).ConfigureAwait(false);
			rawCapabilities = [.. buffer[..length]];
			if (logger.IsEnabled(LogLevel.Information))
			{
				logger.MonitorRetrievedCapabilities(new MonitorId(edid.VendorId, edid.ProductId).ToString()!, Encoding.UTF8.GetString(rawCapabilities.AsSpan()));
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}

		if (TryGetMonitorDefinition(deviceId, out var definition))
		{
			if (definition.Name is not null) friendlyName = definition.Name;
			// NB: We do completely override the capabilities string if a value is provided.
			// This can be a simpler way of defining the capabilities of a monitor. e.g. if it doesn't provide a capabilities string, or if the built-in one is broken.
			if (!definition.Capabilities.IsDefault) rawCapabilities = definition.Capabilities;
		}

		var vcpCodesToIgnore = !definition.IgnoredCapabilitiesVcpCodes.IsDefaultOrEmpty ?
			new HashSet<byte>(ImmutableCollectionsMarshal.AsArray(definition.IgnoredCapabilitiesVcpCodes)!) :
			null;

		byte brightnessVcpCode = 0;
		byte contrastVcpCode = 0;
		byte audioVolumeVcpCode = 0;
		byte inputSelectVcpCode = 0;

		ImmutableArray<NonContinuousValueDescription>.Builder inputSourceBuilder = ImmutableArray.CreateBuilder<NonContinuousValueDescription>();

		if (MonitorCapabilities.TryParse(rawCapabilities.AsSpan(), out var capabilities))
		{
			features |= SupportedFeatures.Capabilities;

			if (!definition.IgnoreAllCapabilitiesVcpCodes)
			{
				foreach (var capability in capabilities.SupportedVcpCommands)
				{
					// Ignore some VCP codes if they are specifically indicated to be ignored.
					// This can be useful if some features are not properly mapped by the monitor.
					if (vcpCodesToIgnore?.Contains(capability.VcpCode) == true) continue;
					if (capability.VcpCode == (byte)VcpCode.Luminance)
					{
						features |= SupportedFeatures.Brightness;
						brightnessVcpCode = capability.VcpCode;
					}
					else if (capability.VcpCode == (byte)VcpCode.Contrast)
					{
						features |= SupportedFeatures.Contrast;
						contrastVcpCode = capability.VcpCode;
					}
					else if (capability.VcpCode == (byte)VcpCode.AudioSpeakerVolume)
					{
						features |= SupportedFeatures.AudioVolume;
						audioVolumeVcpCode = capability.VcpCode;
					}
					else if (capability.VcpCode == (byte)VcpCode.InputSelect)
					{
						if (!capability.NonContinuousValues.IsDefaultOrEmpty)
						{
							features |= SupportedFeatures.InputSelect;
							inputSelectVcpCode = capability.VcpCode;

							foreach (var value in capability.NonContinuousValues)
							{
								Guid nameId;
								if (value.Name is null)
								{
									nameId = value.Value switch
									{
										// VGA 1
										0x01 => new Guid(0x4db28e37, 0x6491, 0x47f8, 0x9c, 0x2d, 0x89, 0x5c, 0xe4, 0xdd, 0xb6, 0xa3),
										// VGA 2
										0x02 => new Guid(0x6b8b64f7, 0xf4e4, 0x4f17, 0xad, 0x48, 0x70, 0x97, 0x86, 0x9b, 0x68, 0xac),
										// DVI 1
										0x03 => new Guid(0x0e8cad25, 0xbd0e, 0x47de, 0xaa, 0xba, 0x22, 0xc6, 0xfa, 0xcd, 0x4a, 0xfd),
										// DVI 2
										0x04 => new Guid(0x16454f19, 0xd79a, 0x45f1, 0x90, 0xa6, 0x6e, 0x3a, 0xd3, 0x98, 0x45, 0x15),
										// DP 1
										0x0F => new Guid(0x1dacbc30, 0x7fd2, 0x41af, 0x8c, 0x1f, 0x9a, 0x3b, 0xc9, 0x88, 0x30, 0x02),
										// DP 2
										0x10 => new Guid(0xa6da60f2, 0x5502, 0x4500, 0xb7, 0x56, 0x5b, 0x62, 0x16, 0xe6, 0x7e, 0xa6),
										// HDMI 1
										0x11 => new Guid(0xff7b1ba4, 0x6e79, 0x4339, 0xa2, 0x5d, 0x1f, 0x63, 0x4f, 0x88, 0xf5, 0xa6),
										// HDMI 2
										0x12 => new Guid(0x8803f0ec, 0x5786, 0x4010, 0x93, 0xb5, 0x8d, 0xcb, 0x20, 0x0e, 0x81, 0x92),
										_ => default,
									};
								}
								else
								{
									nameId = default;
								}
								inputSourceBuilder.Add(new(value.Value, nameId, value.Name));
							}
						}
					}
				}
			}
		}

		if (!definition.OverriddenFeatures.IsDefaultOrEmpty)
		{
			foreach (var feature in definition.OverriddenFeatures)
			{
				switch (feature.Feature)
				{
				case MonitorFeature.Brightness:
					features |= SupportedFeatures.Brightness;
					brightnessVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.Contrast:
					features |= SupportedFeatures.Contrast;
					contrastVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.AudioVolume:
					features |= SupportedFeatures.AudioVolume;
					audioVolumeVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.InputSelect:
					features |= SupportedFeatures.InputSelect;
					inputSelectVcpCode = feature.VcpCode;

					inputSourceBuilder.Clear();
					foreach (var valueDefinition in feature.DiscreteValues)
					{
						inputSourceBuilder.Add(new(valueDefinition.Value, valueDefinition.NameStringId.GetValueOrDefault(), null));
					}
					break;
				}
			}
		}

		var inputSources = inputSourceBuilder.DrainToImmutable();
		HashSet<ushort>? validInputSources = null;
		if (inputSources.Length > 0)
		{
			validInputSources = [];
			foreach (var source in inputSources)
			{
				if (!validInputSources.Add(source.Value))
				{
					throw new InvalidOperationException("Duplicate input source ID detected.");
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
				rawCapabilities.AsMemory(),
				capabilities,
				brightnessVcpCode,
				contrastVcpCode,
				audioVolumeVcpCode,
				inputSelectVcpCode,
				inputSources,
				validInputSources,
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
		InputSelect = 0x00000010,
	}

	private sealed class MonitorFeatureSet : IDeviceFeatureSet<IMonitorDeviceFeature>
	{
		private readonly GenericMonitorDriver _driver;
		private Dictionary<Type, IMonitorDeviceFeature>? _cachedFeatureDictionary;

		public bool IsEmpty => _driver._supportedFeatures == SupportedFeatures.None;

		public int Count
		{
			get
			{
				int count = 0;

				var supportedFeatures = _driver._supportedFeatures;
				if ((supportedFeatures & SupportedFeatures.Capabilities) != 0) count += 2;
				if ((supportedFeatures & SupportedFeatures.Brightness) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.Contrast) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.AudioVolume) != 0) count++;

				return count;
			}
		}

		public MonitorFeatureSet(GenericMonitorDriver driver) => _driver = driver;

		IMonitorDeviceFeature? IDeviceFeatureSet<IMonitorDeviceFeature>.this[Type type]
			=> (_cachedFeatureDictionary ??= new(this))[type];

		T? IDeviceFeatureSet<IMonitorDeviceFeature>.GetFeature<T>() where T : class
		{
			var supportedFeatures = _driver._supportedFeatures;
			if (typeof(T) == typeof(IMonitorCapabilitiesFeature) && (supportedFeatures & SupportedFeatures.Capabilities) != 0 ||
				typeof(T) == typeof(IMonitorRawCapabilitiesFeature) && (supportedFeatures & SupportedFeatures.Capabilities) != 0 ||
				typeof(T) == typeof(IMonitorBrightnessFeature) && (supportedFeatures & SupportedFeatures.Brightness) != 0 ||
				typeof(T) == typeof(IMonitorContrastFeature) && (supportedFeatures & SupportedFeatures.Contrast) != 0 ||
				typeof(T) == typeof(IMonitorSpeakerAudioVolumeFeature) && (supportedFeatures & SupportedFeatures.AudioVolume) != 0 ||
				typeof(T) == typeof(IMonitorInputSelectFeature) && (supportedFeatures & SupportedFeatures.InputSelect) != 0)
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
			if ((supportedFeatures & SupportedFeatures.InputSelect) != 0) yield return new(typeof(IMonitorInputSelectFeature), _driver);
		}

		IEnumerator IEnumerable.GetEnumerator() => Unsafe.As<IEnumerable<KeyValuePair<Type, IMonitorDeviceFeature>>>(this).GetEnumerator();
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private readonly DisplayDataChannel _ddc;
	private readonly SupportedFeatures _supportedFeatures;
	private readonly ReadOnlyMemory<byte> _rawCapabilities;
	private readonly MonitorCapabilities? _capabilities;
	private readonly DeviceId _deviceId;

	private readonly byte _brightnessVcpCode;
	private readonly byte _contrastVcpCode;
	private readonly byte _audioVolumeVcpCode;
	private readonly byte _inputSelectVcpCode;
	private readonly ImmutableArray<NonContinuousValueDescription> _inputSources;
	private readonly HashSet<ushort>? _validInputSources;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	DeviceId IDeviceIdFeature.DeviceId => _deviceId;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	protected GenericMonitorDriver
	(
		DisplayDataChannel ddc,
		SupportedFeatures supportedFeatures,
		ReadOnlyMemory<byte> rawCapabilities,
		MonitorCapabilities? capabilities,
		byte brightnessVcpCode,
		byte contrastVcpCode,
		byte audioVolumeVcpCode,
		byte inputSelectVcpCode,
		ImmutableArray<NonContinuousValueDescription> inputSources,
		HashSet<ushort>? validInputSources,
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

		_brightnessVcpCode = brightnessVcpCode;
		_contrastVcpCode = contrastVcpCode;
		_contrastVcpCode = contrastVcpCode;
		_audioVolumeVcpCode = audioVolumeVcpCode;
		_inputSelectVcpCode = inputSelectVcpCode;
		_inputSources = inputSources;
		_validInputSources = validInputSources;

		_genericFeatures = configurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature>(this);

		_monitorFeatures = new MonitorFeatureSet(this);
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private void EnsureSupportedFeatures(SupportedFeatures features)
	{
		if ((_supportedFeatures & features) != features) throw new NotSupportedException("The requested feature is not supported by this monitor.");
	}

	private async ValueTask<ContinuousValue> GetVcpAsync(SupportedFeatures features, byte code, CancellationToken cancellationToken)
	{
		EnsureSupportedFeatures(features);
		var reply = await _ddc.GetVcpFeatureAsync(code, cancellationToken).ConfigureAwait(false);
		return new ContinuousValue(reply.CurrentValue, 0, reply.MaximumValue);
	}

	private async ValueTask<ushort> GetNonContinuousVcpAsync(SupportedFeatures features, byte code, CancellationToken cancellationToken)
	{
		EnsureSupportedFeatures(features);
		var reply = await _ddc.GetVcpFeatureAsync(code, cancellationToken).ConfigureAwait(false);
		return reply.CurrentValue;
	}

	private async ValueTask SetVcpAsync(SupportedFeatures features, byte code, ushort value, CancellationToken cancellationToken)
	{
		EnsureSupportedFeatures(features);
		await _ddc.SetVcpFeatureAsync(code, value, cancellationToken).ConfigureAwait(false);
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

	ValueTask<ContinuousValue> IMonitorBrightnessFeature.GetBrightnessAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.Brightness, _brightnessVcpCode, cancellationToken);
	ValueTask IMonitorBrightnessFeature.SetBrightnessAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.Brightness, _brightnessVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorContrastFeature.GetContrastAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.Contrast, _contrastVcpCode, cancellationToken);
	ValueTask IMonitorContrastFeature.SetContrastAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.Contrast, _contrastVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorSpeakerAudioVolumeFeature.GetVolumeAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.AudioVolume, _audioVolumeVcpCode, cancellationToken);
	ValueTask IMonitorSpeakerAudioVolumeFeature.SetVolumeAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.AudioVolume, _audioVolumeVcpCode, value, cancellationToken);

	ImmutableArray<NonContinuousValueDescription> IMonitorInputSelectFeature.InputSources => _inputSources;

	ValueTask<ushort> IMonitorInputSelectFeature.GetInputSourceAsync(CancellationToken cancellationToken)
		=> GetNonContinuousVcpAsync(SupportedFeatures.InputSelect, _inputSelectVcpCode, cancellationToken);

	ValueTask IMonitorInputSelectFeature.SetInputSourceAsync(ushort sourceId, CancellationToken cancellationToken)
	{
		if (_validInputSources is null || !_validInputSources.Contains(sourceId))
		{
			throw new ArgumentOutOfRangeException(nameof(sourceId), "The specified source ID is not allowed by the definition.");
		}
		return SetVcpAsync(SupportedFeatures.InputSelect, _inputSelectVcpCode, sourceId, cancellationToken);
	}
}
