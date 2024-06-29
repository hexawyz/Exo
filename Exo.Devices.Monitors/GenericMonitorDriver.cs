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
	IMonitorInputSelectFeature,
	IMonitorRedVideoGainFeature,
	IMonitorGreenVideoGainFeature,
	IMonitorBlueVideoGainFeature,
	IMonitorRedSixAxisSaturationControlFeature,
	IMonitorYellowSixAxisSaturationControlFeature,
	IMonitorGreenSixAxisSaturationControlFeature,
	IMonitorCyanSixAxisSaturationControlFeature,
	IMonitorBlueSixAxisSaturationControlFeature,
	IMonitorMagentaSixAxisSaturationControlFeature,
	IMonitorRedSixAxisHueControlFeature,
	IMonitorYellowSixAxisHueControlFeature,
	IMonitorGreenSixAxisHueControlFeature,
	IMonitorCyanSixAxisHueControlFeature,
	IMonitorBlueSixAxisHueControlFeature,
	IMonitorMagentaSixAxisHueControlFeature
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
		byte redVideoGainVcpCode = 0;
		byte greenVideoGainVcpCode = 0;
		byte blueVideoGainVcpCode = 0;
		byte redSixAxisSaturationControlVcpCode = 0;
		byte yellowSixAxisSaturationControlVcpCode = 0;
		byte greenSixAxisSaturationControlVcpCode = 0;
		byte cyanSixAxisSaturationControlVcpCode = 0;
		byte blueSixAxisSaturationControlVcpCode = 0;
		byte magentaSixAxisSaturationControlVcpCode = 0;
		byte redSixAxisHueControlVcpCode = 0;
		byte yellowSixAxisHueControlVcpCode = 0;
		byte greenSixAxisHueControlVcpCode = 0;
		byte cyanSixAxisHueControlVcpCode = 0;
		byte blueSixAxisHueControlVcpCode = 0;
		byte magentaSixAxisHueControlVcpCode = 0;

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
					switch (capability.VcpCode)
					{
					case (byte)VcpCode.Luminance:
						features |= SupportedFeatures.Brightness;
						brightnessVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.Contrast:
						features |= SupportedFeatures.Contrast;
						contrastVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.AudioSpeakerVolume:
						features |= SupportedFeatures.AudioVolume;
						audioVolumeVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.InputSelect:
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
						break;
					case (byte)VcpCode.VideoGainRed:
						features |= SupportedFeatures.VideoGainRed;
						redVideoGainVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.VideoGainGreen:
						features |= SupportedFeatures.VideoGainGreen;
						greenVideoGainVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.VideoGainBlue:
						features |= SupportedFeatures.VideoGainBlue;
						blueVideoGainVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisSaturationControlRed:
						features |= SupportedFeatures.SixAxisSaturationControlRed;
						redSixAxisSaturationControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisSaturationControlYellow:
						features |= SupportedFeatures.SixAxisSaturationControlYellow;
						yellowSixAxisSaturationControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisSaturationControlGreen:
						features |= SupportedFeatures.SixAxisSaturationControlGreen;
						greenSixAxisSaturationControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisSaturationControlCyan:
						features |= SupportedFeatures.SixAxisSaturationControlCyan;
						cyanSixAxisSaturationControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisSaturationControlBlue:
						features |= SupportedFeatures.SixAxisSaturationControlBlue;
						blueSixAxisSaturationControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisSaturationControlMagenta:
						features |= SupportedFeatures.SixAxisSaturationControlMagenta;
						magentaSixAxisSaturationControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisColorControlRed:
						features |= SupportedFeatures.SixAxisHueControlRed;
						redSixAxisHueControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisColorControlYellow:
						features |= SupportedFeatures.SixAxisHueControlYellow;
						yellowSixAxisHueControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisColorControlGreen:
						features |= SupportedFeatures.SixAxisHueControlGreen;
						greenSixAxisHueControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisColorControlCyan:
						features |= SupportedFeatures.SixAxisHueControlCyan;
						cyanSixAxisHueControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisColorControlBlue:
						features |= SupportedFeatures.SixAxisHueControlBlue;
						blueSixAxisHueControlVcpCode = capability.VcpCode;
						break;
					case (byte)VcpCode.SixAxisColorControlMagenta:
						features |= SupportedFeatures.SixAxisHueControlMagenta;
						magentaSixAxisHueControlVcpCode = capability.VcpCode;
						break;
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
				case MonitorFeature.VideoGainRed:
					features |= SupportedFeatures.VideoGainRed;
					redVideoGainVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.VideoGainGreen:
					features |= SupportedFeatures.VideoGainGreen;
					greenVideoGainVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.VideoGainBlue:
					features |= SupportedFeatures.VideoGainBlue;
					blueVideoGainVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisSaturationControlRed:
					features |= SupportedFeatures.SixAxisSaturationControlRed;
					redSixAxisSaturationControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisSaturationControlYellow:
					features |= SupportedFeatures.SixAxisSaturationControlYellow;
					yellowSixAxisSaturationControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisSaturationControlGreen:
					features |= SupportedFeatures.SixAxisSaturationControlGreen;
					greenSixAxisSaturationControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisSaturationControlCyan:
					features |= SupportedFeatures.SixAxisSaturationControlCyan;
					cyanSixAxisSaturationControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisSaturationControlBlue:
					features |= SupportedFeatures.SixAxisSaturationControlBlue;
					blueSixAxisSaturationControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisSaturationControlMagenta:
					features |= SupportedFeatures.SixAxisSaturationControlMagenta;
					magentaSixAxisSaturationControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisHueControlRed:
					features |= SupportedFeatures.SixAxisHueControlRed;
					redSixAxisHueControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisHueControlYellow:
					features |= SupportedFeatures.SixAxisHueControlYellow;
					yellowSixAxisHueControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisHueControlGreen:
					features |= SupportedFeatures.SixAxisHueControlGreen;
					greenSixAxisHueControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisHueControlCyan:
					features |= SupportedFeatures.SixAxisHueControlCyan;
					cyanSixAxisHueControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisHueControlBlue:
					features |= SupportedFeatures.SixAxisHueControlBlue;
					blueSixAxisHueControlVcpCode = feature.VcpCode;
					break;
				case MonitorFeature.SixAxisHueControlMagenta:
					features |= SupportedFeatures.SixAxisHueControlMagenta;
					magentaSixAxisHueControlVcpCode = feature.VcpCode;
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
				redVideoGainVcpCode,
				greenVideoGainVcpCode,
				blueVideoGainVcpCode,
				redSixAxisSaturationControlVcpCode,
				yellowSixAxisSaturationControlVcpCode,
				greenSixAxisSaturationControlVcpCode,
				cyanSixAxisSaturationControlVcpCode,
				blueSixAxisSaturationControlVcpCode,
				magentaSixAxisSaturationControlVcpCode,
				redSixAxisHueControlVcpCode,
				yellowSixAxisHueControlVcpCode,
				greenSixAxisHueControlVcpCode,
				cyanSixAxisHueControlVcpCode,
				blueSixAxisHueControlVcpCode,
				magentaSixAxisHueControlVcpCode,
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
		VideoGainRed = 0x00000020,
		VideoGainGreen = 0x00000040,
		VideoGainBlue = 0x00000080,
		SixAxisSaturationControlRed = 0x00000100,
		SixAxisSaturationControlYellow = 0x00000200,
		SixAxisSaturationControlGreen = 0x00000400,
		SixAxisSaturationControlCyan = 0x00000800,
		SixAxisSaturationControlBlue = 0x00001000,
		SixAxisSaturationControlMagenta = 0x00002000,
		SixAxisHueControlRed = 0x00004000,
		SixAxisHueControlYellow = 0x00008000,
		SixAxisHueControlGreen = 0x00010000,
		SixAxisHueControlCyan = 0x00020000,
		SixAxisHueControlBlue = 0x00040000,
		SixAxisHueControlMagenta = 0x00080000,
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
				if ((supportedFeatures & SupportedFeatures.InputSelect) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.VideoGainRed) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.VideoGainGreen) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.VideoGainBlue) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlRed) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlYellow) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlGreen) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlCyan) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlBlue) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlMagenta) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisHueControlRed) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisHueControlYellow) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisHueControlGreen) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisHueControlCyan) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisHueControlBlue) != 0) count++;
				if ((supportedFeatures & SupportedFeatures.SixAxisHueControlMagenta) != 0) count++;

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
				typeof(T) == typeof(IMonitorInputSelectFeature) && (supportedFeatures & SupportedFeatures.InputSelect) != 0 ||
				typeof(T) == typeof(IMonitorRedVideoGainFeature) && (supportedFeatures & SupportedFeatures.VideoGainRed) != 0 ||
				typeof(T) == typeof(IMonitorGreenVideoGainFeature) && (supportedFeatures & SupportedFeatures.VideoGainGreen) != 0 ||
				typeof(T) == typeof(IMonitorBlueVideoGainFeature) && (supportedFeatures & SupportedFeatures.VideoGainBlue) != 0 ||
				typeof(T) == typeof(IMonitorRedSixAxisSaturationControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisSaturationControlRed) != 0 ||
				typeof(T) == typeof(IMonitorYellowSixAxisSaturationControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisSaturationControlYellow) != 0 ||
				typeof(T) == typeof(IMonitorGreenSixAxisSaturationControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisSaturationControlGreen) != 0 ||
				typeof(T) == typeof(IMonitorCyanSixAxisSaturationControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisSaturationControlCyan) != 0 ||
				typeof(T) == typeof(IMonitorBlueSixAxisSaturationControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisSaturationControlBlue) != 0 ||
				typeof(T) == typeof(IMonitorMagentaSixAxisSaturationControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisSaturationControlMagenta) != 0 ||
				typeof(T) == typeof(IMonitorRedSixAxisHueControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisHueControlRed) != 0 ||
				typeof(T) == typeof(IMonitorYellowSixAxisHueControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisHueControlYellow) != 0 ||
				typeof(T) == typeof(IMonitorGreenSixAxisHueControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisHueControlGreen) != 0 ||
				typeof(T) == typeof(IMonitorCyanSixAxisHueControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisHueControlCyan) != 0 ||
				typeof(T) == typeof(IMonitorBlueSixAxisHueControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisHueControlBlue) != 0 ||
				typeof(T) == typeof(IMonitorMagentaSixAxisHueControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisHueControlMagenta) != 0)
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
			if ((supportedFeatures & SupportedFeatures.VideoGainRed) != 0) yield return new(typeof(IMonitorRedVideoGainFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.VideoGainGreen) != 0) yield return new(typeof(IMonitorGreenVideoGainFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.VideoGainBlue) != 0) yield return new(typeof(IMonitorBlueVideoGainFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlRed) != 0) yield return new(typeof(IMonitorRedSixAxisSaturationControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlYellow) != 0) yield return new(typeof(IMonitorYellowSixAxisSaturationControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlGreen) != 0) yield return new(typeof(IMonitorGreenSixAxisSaturationControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlCyan) != 0) yield return new(typeof(IMonitorCyanSixAxisSaturationControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlBlue) != 0) yield return new(typeof(IMonitorBlueSixAxisSaturationControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisSaturationControlMagenta) != 0) yield return new(typeof(IMonitorMagentaSixAxisSaturationControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisHueControlRed) != 0) yield return new(typeof(IMonitorRedSixAxisHueControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisHueControlYellow) != 0) yield return new(typeof(IMonitorYellowSixAxisHueControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisHueControlGreen) != 0) yield return new(typeof(IMonitorGreenSixAxisHueControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisHueControlCyan) != 0) yield return new(typeof(IMonitorCyanSixAxisHueControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisHueControlBlue) != 0) yield return new(typeof(IMonitorBlueSixAxisHueControlFeature), _driver);
			if ((supportedFeatures & SupportedFeatures.SixAxisHueControlMagenta) != 0) yield return new(typeof(IMonitorMagentaSixAxisHueControlFeature), _driver);
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
	private readonly byte _redVideoGainVcpCode;
	private readonly byte _greenVideoGainVcpCode;
	private readonly byte _blueVideoGainVcpCode;
	private readonly byte _redSixAxisSaturationControlVcpCode;
	private readonly byte _yellowSixAxisSaturationControlVcpCode;
	private readonly byte _greenSixAxisSaturationControlVcpCode;
	private readonly byte _cyanSixAxisSaturationControlVcpCode;
	private readonly byte _blueSixAxisSaturationControlVcpCode;
	private readonly byte _magentaSixAxisSaturationControlVcpCode;
	private readonly byte _redSixAxisHueControlVcpCode;
	private readonly byte _yellowSixAxisHueControlVcpCode;
	private readonly byte _greenSixAxisHueControlVcpCode;
	private readonly byte _cyanSixAxisHueControlVcpCode;
	private readonly byte _blueSixAxisHueControlVcpCode;
	private readonly byte _magentaSixAxisHueControlVcpCode;

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
		byte redVideoGainVcpCode,
		byte greenVideoGainVcpCode,
		byte blueVideoGainVcpCode,
		byte redSixAxisSaturationControlVcpCode,
		byte yellowSixAxisSaturationControlVcpCode,
		byte greenSixAxisSaturationControlVcpCode,
		byte cyanSixAxisSaturationControlVcpCode,
		byte blueSixAxisSaturationControlVcpCode,
		byte magentaSixAxisSaturationControlVcpCode,
		byte redSixAxisHueControlVcpCode,
		byte yellowSixAxisHueControlVcpCode,
		byte greenSixAxisHueControlVcpCode,
		byte cyanSixAxisHueControlVcpCode,
		byte blueSixAxisHueControlVcpCode,
		byte magentaSixAxisHueControlVcpCode,
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
		_redVideoGainVcpCode = redVideoGainVcpCode;
		_greenVideoGainVcpCode = greenVideoGainVcpCode;
		_blueVideoGainVcpCode = blueVideoGainVcpCode;
		_redSixAxisSaturationControlVcpCode = redSixAxisSaturationControlVcpCode;
		_yellowSixAxisSaturationControlVcpCode = yellowSixAxisSaturationControlVcpCode;
		_greenSixAxisSaturationControlVcpCode = greenSixAxisSaturationControlVcpCode;
		_cyanSixAxisSaturationControlVcpCode = cyanSixAxisSaturationControlVcpCode;
		_blueSixAxisSaturationControlVcpCode = blueSixAxisSaturationControlVcpCode;
		_magentaSixAxisSaturationControlVcpCode = magentaSixAxisSaturationControlVcpCode;
		_redSixAxisHueControlVcpCode = redSixAxisHueControlVcpCode;
		_yellowSixAxisHueControlVcpCode = yellowSixAxisHueControlVcpCode;
		_greenSixAxisHueControlVcpCode = greenSixAxisHueControlVcpCode;
		_cyanSixAxisHueControlVcpCode = cyanSixAxisHueControlVcpCode;
		_blueSixAxisHueControlVcpCode = blueSixAxisHueControlVcpCode;
		_magentaSixAxisHueControlVcpCode = magentaSixAxisHueControlVcpCode;
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

	ValueTask<ContinuousValue> IMonitorRedVideoGainFeature.GetRedVideoGainAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.VideoGainRed, _redVideoGainVcpCode, cancellationToken);
	ValueTask IMonitorRedVideoGainFeature.SetRedVideoGainAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.VideoGainRed, _redVideoGainVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorGreenVideoGainFeature.GetGreenVideoGainAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.VideoGainGreen, _greenVideoGainVcpCode, cancellationToken);
	ValueTask IMonitorGreenVideoGainFeature.SetGreenVideoGainAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.VideoGainGreen, _greenVideoGainVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorBlueVideoGainFeature.GetBlueVideoGainAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.VideoGainBlue, _blueVideoGainVcpCode, cancellationToken);
	ValueTask IMonitorBlueVideoGainFeature.SetBlueVideoGainAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.VideoGainBlue, _blueVideoGainVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorRedSixAxisSaturationControlFeature.GetRedSixAxisSaturationControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisSaturationControlRed, _redSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorRedSixAxisSaturationControlFeature.SetRedSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisSaturationControlRed, _redSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorYellowSixAxisSaturationControlFeature.GetYellowSixAxisSaturationControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisSaturationControlYellow, _yellowSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorYellowSixAxisSaturationControlFeature.SetYellowSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisSaturationControlYellow, _yellowSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorGreenSixAxisSaturationControlFeature.GetGreenSixAxisSaturationControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisSaturationControlGreen, _greenSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorGreenSixAxisSaturationControlFeature.SetGreenSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisSaturationControlGreen, _greenSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorCyanSixAxisSaturationControlFeature.GetCyanSixAxisSaturationControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisSaturationControlCyan, _cyanSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorCyanSixAxisSaturationControlFeature.SetCyanSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisSaturationControlCyan, _cyanSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorBlueSixAxisSaturationControlFeature.GetBlueSixAxisSaturationControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisSaturationControlBlue, _blueSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorBlueSixAxisSaturationControlFeature.SetBlueSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisSaturationControlBlue, _blueSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorMagentaSixAxisSaturationControlFeature.GetMagentaSixAxisSaturationControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisSaturationControlMagenta, _magentaSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorMagentaSixAxisSaturationControlFeature.SetMagentaSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisSaturationControlMagenta, _magentaSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorRedSixAxisHueControlFeature.GetRedSixAxisHueControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisHueControlRed, _redSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorRedSixAxisHueControlFeature.SetRedSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisHueControlRed, _redSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorYellowSixAxisHueControlFeature.GetYellowSixAxisHueControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisHueControlYellow, _yellowSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorYellowSixAxisHueControlFeature.SetYellowSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisHueControlYellow, _yellowSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorGreenSixAxisHueControlFeature.GetGreenSixAxisHueControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisHueControlGreen, _greenSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorGreenSixAxisHueControlFeature.SetGreenSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisHueControlGreen, _greenSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorCyanSixAxisHueControlFeature.GetCyanSixAxisHueControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisHueControlCyan, _cyanSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorCyanSixAxisHueControlFeature.SetCyanSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisHueControlCyan, _cyanSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorBlueSixAxisHueControlFeature.GetBlueSixAxisHueControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisHueControlBlue, _blueSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorBlueSixAxisHueControlFeature.SetBlueSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisHueControlBlue, _blueSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorMagentaSixAxisHueControlFeature.GetMagentaSixAxisHueControlAsync(CancellationToken cancellationToken) => GetVcpAsync(SupportedFeatures.SixAxisHueControlMagenta, _magentaSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorMagentaSixAxisHueControlFeature.SetMagentaSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken) => SetVcpAsync(SupportedFeatures.SixAxisHueControlMagenta, _magentaSixAxisHueControlVcpCode, value, cancellationToken);
}
