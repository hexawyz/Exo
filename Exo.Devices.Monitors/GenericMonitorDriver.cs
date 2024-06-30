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
	IMonitorMagentaSixAxisHueControlFeature,
	IMonitorOsdLanguageFeature
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
		byte osdLanguageVcpCode = 0;

		ImmutableArray<NonContinuousValueDescription>.Builder inputSourceBuilder = ImmutableArray.CreateBuilder<NonContinuousValueDescription>();
		ImmutableArray<NonContinuousValueDescription>.Builder osdLanguageBuilder = ImmutableArray.CreateBuilder<NonContinuousValueDescription>();

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
					case (byte)VcpCode.OsdLanguage:
						if (!capability.NonContinuousValues.IsDefaultOrEmpty)
						{
							features |= SupportedFeatures.OsdLanguage;
							osdLanguageVcpCode = capability.VcpCode;

							foreach (var value in capability.NonContinuousValues)
							{
								Guid nameId;
								if (value.Name is null)
								{
									// In principle, the language name strings should all be defined in the global strings, so that they can be reused across different needs.
									nameId = value.Value switch
									{
										// Chinese (Traditional)
										0x01 => new Guid(0xE9D6E301, 0xDE10, 0x4F5A, 0x91, 0x0E, 0x5A, 0xA8, 0x0C, 0xF0, 0x57, 0xD1),
										// English
										0x02 => new Guid(0x3D7555D0, 0x345A, 0x49F1, 0x90, 0x3E, 0xA1, 0x5D, 0x35, 0xB0, 0xF5, 0xEC),
										// French
										0x03 => new Guid(0xCC5D6FB5, 0x0653, 0x49BB, 0x85, 0xCC, 0x4A, 0xE2, 0xCA, 0x82, 0xF4, 0x87),
										// German
										0x04 => new Guid(0x1AF16A76, 0xE293, 0x4096, 0xA9, 0xD7, 0x9E, 0x9E, 0xFF, 0x68, 0xB5, 0x91),
										// Italian
										0x05 => new Guid(0xF12057BC, 0x08CE, 0x446D, 0x9D, 0x25, 0xB5, 0x96, 0x5B, 0x61, 0x36, 0xAB),
										// Japanese
										0x06 => new Guid(0x9EF9C042, 0xBFBB, 0x4B72, 0x9C, 0x24, 0x4D, 0xA5, 0x6E, 0x1F, 0x56, 0xBE),
										// Korean
										0x07 => new Guid(0x12DF152A, 0x1065, 0x4200, 0x96, 0x6F, 0x2B, 0xE7, 0x74, 0x10, 0xFD, 0x13),
										// Portuguese (Portugal)
										0x08 => new Guid(0xE3E65711, 0xAAE3, 0x4F2D, 0xAA, 0x95, 0x09, 0x2C, 0xFC, 0x10, 0x58, 0x72),
										// Russian
										0x09 => new Guid(0x5AD769BF, 0x3AA1, 0x49A1, 0xA9, 0xF9, 0x6F, 0x0E, 0xC1, 0xB4, 0x3D, 0xD2),
										// Spanish
										0x0A => new Guid(0xF4BCB574, 0xA61D, 0x4B25, 0x82, 0x4B, 0x6E, 0x86, 0x5D, 0x74, 0x27, 0xFE),
										// Swedish
										0x0B => new Guid(0x88307C6F, 0xDA3E, 0x424B, 0xBE, 0xF0, 0x23, 0xA2, 0x52, 0xE5, 0xB2, 0x6D),
										// Turkish
										0x0C => new Guid(0xF2FC7A26, 0xB392, 0x4C13, 0xA8, 0x6E, 0x47, 0xB2, 0xE3, 0x90, 0xCA, 0xF8),
										// Chinese (Simplified)
										0x0D => new Guid(0x4B0F748A, 0xCC4A, 0x41EA, 0xA5, 0x9B, 0x95, 0x49, 0xDD, 0x61, 0x9E, 0x76),
										// Portuguese (Brazil)
										0x0E => new Guid(0x9318646F, 0x67AA, 0x4FAE, 0x95, 0xFC, 0x36, 0x18, 0xC2, 0xF2, 0x77, 0x7B),
										// Arabic
										0x0F => new Guid(0xAC934765, 0x0EB0, 0x4AC6, 0xB0, 0x85, 0x88, 0x17, 0x7B, 0x1E, 0x2D, 0x84),
										// Bulgarian
										0x10 => new Guid(0x49A21314, 0x8315, 0x47A5, 0xB0, 0x1B, 0x6C, 0x7E, 0x50, 0x40, 0x39, 0x21),
										// Croatian
										0x11 => new Guid(0x5506A84A, 0x4EBF, 0x46F5, 0xB6, 0x11, 0x04, 0x7C, 0xB8, 0x63, 0x1A, 0xB9),
										// Czech
										0x12 => new Guid(0xF73A11CB, 0xEC92, 0x42A2, 0x83, 0xB9, 0x5D, 0x89, 0xA8, 0x11, 0xD3, 0x6E),
										// Danish
										0x13 => new Guid(0xED6EF567, 0xB80A, 0x40D0, 0x95, 0x51, 0x38, 0xF3, 0x99, 0x55, 0xC4, 0xC1),
										// Dutch
										0x14 => new Guid(0xC42DD145, 0x2882, 0x4B31, 0x90, 0xD0, 0xA4, 0x7A, 0x82, 0x2E, 0xD1, 0xCD),
										// Estonian
										0x15 => new Guid(0x3EE2C2C1, 0x1DA2, 0x434B, 0x80, 0xD2, 0x24, 0x63, 0xA5, 0x22, 0x73, 0xF5),
										// Finnish
										0x16 => new Guid(0x42177650, 0x1189, 0x414E, 0x9F, 0x67, 0x15, 0xA0, 0xA4, 0x84, 0xA1, 0xF2),
										// Greek
										0x17 => new Guid(0x9342A747, 0xF7E4, 0x4F79, 0xB0, 0xCE, 0x5F, 0xB0, 0xC8, 0x04, 0x01, 0x26),
										// Hebrew
										0x18 => new Guid(0x7BED3E7D, 0x9DF4, 0x41A9, 0x94, 0xEA, 0x4B, 0xDE, 0x84, 0xDB, 0x23, 0xA2),
										// Hindi
										0x19 => new Guid(0x3F31973D, 0x899A, 0x46F3, 0xA2, 0x49, 0xDB, 0x6B, 0x9C, 0x89, 0x5F, 0x4A),
										// Hungarian
										0x1A => new Guid(0xE3720708, 0x932F, 0x478F, 0x9C, 0x7F, 0x59, 0xFD, 0x4D, 0x3E, 0x46, 0xBF),
										// Latvian
										0x1B => new Guid(0x3D1908B1, 0x95D4, 0x4AB4, 0x9B, 0x56, 0xC8, 0x66, 0x2E, 0x0E, 0x9A, 0xD1),
										// Lithuanian
										0x1C => new Guid(0xFDB186F3, 0x4DD1, 0x44FD, 0x9B, 0xFA, 0xC7, 0x23, 0x7D, 0x03, 0x5A, 0x09),
										// Norwegian
										0x1D => new Guid(0xF550EB5B, 0xC345, 0x44DB, 0xA3, 0x41, 0x26, 0xFB, 0x3D, 0x50, 0xC0, 0x4B),
										// Polish
										0x1E => new Guid(0x11801B1C, 0x2084, 0x49CE, 0xA9, 0x65, 0x19, 0xA5, 0xC3, 0xB3, 0x7A, 0x9E),
										// Romanian
										0x1F => new Guid(0x8A812B6B, 0x9D8C, 0x4281, 0xBC, 0x9C, 0xA7, 0x9F, 0xCE, 0x73, 0x63, 0x66),
										// Serbian
										0x20 => new Guid(0x8912B5E0, 0x119D, 0x49DC, 0x9F, 0x33, 0x87, 0x2E, 0x7E, 0x37, 0xE7, 0x29),
										// Slovak
										0x21 => new Guid(0x5317048D, 0x64DD, 0x4240, 0xAE, 0x2A, 0xD0, 0x78, 0x7C, 0x04, 0xE6, 0x33),
										// Slovenian
										0x22 => new Guid(0x78632023, 0x055C, 0x4447, 0x8E, 0x94, 0x63, 0x19, 0xC9, 0xFD, 0xED, 0x96),
										// Thai
										0x23 => new Guid(0xE64BD133, 0xB3A1, 0x4D51, 0xBF, 0x9C, 0x63, 0xC0, 0xC4, 0xD0, 0xD9, 0xA8),
										// Ukrainian
										0x24 => new Guid(0x5A78CC6C, 0xBF97, 0x424E, 0xB5, 0x20, 0xFB, 0x00, 0x4D, 0x10, 0x09, 0xE9),
										// Vietnamese
										0x25 => new Guid(0x6A8FBD37, 0x8ED5, 0x4ADE, 0xAF, 0x15, 0xCC, 0xF2, 0xE6, 0x2F, 0xC8, 0xB7),
										_ => default,
									};
								}
								else
								{
									nameId = default;
								}
								osdLanguageBuilder.Add(new(value.Value, nameId, value.Name));
							}
						}
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
					if (!feature.DiscreteValues.IsDefaultOrEmpty)
					{
						foreach (var valueDefinition in feature.DiscreteValues)
						{
							inputSourceBuilder.Add(new(valueDefinition.Value, valueDefinition.NameStringId.GetValueOrDefault(), null));
						}
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
				case MonitorFeature.OsdLanguage:
					features |= SupportedFeatures.OsdLanguage;
					osdLanguageVcpCode = feature.VcpCode;

					osdLanguageBuilder.Clear();
					if (!feature.DiscreteValues.IsDefaultOrEmpty)
					{
						foreach (var valueDefinition in feature.DiscreteValues)
						{
							osdLanguageBuilder.Add(new(valueDefinition.Value, valueDefinition.NameStringId.GetValueOrDefault(), null));
						}
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
			foreach (var description in inputSources)
			{
				if (!validInputSources.Add(description.Value))
				{
					throw new InvalidOperationException("Duplicate input source ID detected.");
				}
			}
		}

		var osdLanguages = osdLanguageBuilder.DrainToImmutable();
		HashSet<ushort>? validOsdLanguages = null;
		if (osdLanguages.Length > 0)
		{
			validOsdLanguages = [];
			foreach (var description in osdLanguages)
			{
				if (!validOsdLanguages.Add(description.Value))
				{
					throw new InvalidOperationException("Duplicate OSD language ID detected.");
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
				osdLanguageVcpCode,
				inputSources,
				validInputSources,
				osdLanguages,
				validOsdLanguages,
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
		OsdLanguage = 0x00100000,
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
				if ((supportedFeatures & SupportedFeatures.OsdLanguage) != 0) count++;

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
				typeof(T) == typeof(IMonitorMagentaSixAxisHueControlFeature) && (supportedFeatures & SupportedFeatures.SixAxisHueControlMagenta) != 0 ||
				typeof(T) == typeof(IMonitorOsdLanguageFeature) && (supportedFeatures & SupportedFeatures.OsdLanguage) != 0)
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
			if ((supportedFeatures & SupportedFeatures.OsdLanguage) != 0) yield return new(typeof(IMonitorOsdLanguageFeature), _driver);
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

	private readonly byte _osdLanguageVcpCode;

	private readonly ImmutableArray<NonContinuousValueDescription> _inputSources;
	private readonly HashSet<ushort>? _validInputSources;

	private readonly ImmutableArray<NonContinuousValueDescription> _osdLanguages;
	private readonly HashSet<ushort>? _validOsdLanguages;

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
		byte osdLanguageVcpCode,
		ImmutableArray<NonContinuousValueDescription> inputSources,
		HashSet<ushort>? validInputSources,
		ImmutableArray<NonContinuousValueDescription> osdLanguages,
		HashSet<ushort>? validOsdLanguages,
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
		_osdLanguageVcpCode = osdLanguageVcpCode;
		_inputSources = inputSources;
		_validInputSources = validInputSources;
		_osdLanguages = osdLanguages;
		_validOsdLanguages = validOsdLanguages;

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

	private async ValueTask SetVcpAsync(SupportedFeatures features, HashSet<ushort>? supportedValues, byte code, ushort value, CancellationToken cancellationToken)
	{
		EnsureSupportedFeatures(features);
		if (supportedValues is null || !supportedValues.Contains(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), "The specified value is not allowed by the definition.");
		}
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

	ValueTask<ContinuousValue> IMonitorBrightnessFeature.GetBrightnessAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.Brightness, _brightnessVcpCode, cancellationToken);
	ValueTask IMonitorBrightnessFeature.SetBrightnessAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.Brightness, _brightnessVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorContrastFeature.GetContrastAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.Contrast, _contrastVcpCode, cancellationToken);
	ValueTask IMonitorContrastFeature.SetContrastAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.Contrast, _contrastVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorSpeakerAudioVolumeFeature.GetVolumeAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.AudioVolume, _audioVolumeVcpCode, cancellationToken);
	ValueTask IMonitorSpeakerAudioVolumeFeature.SetVolumeAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.AudioVolume, _audioVolumeVcpCode, value, cancellationToken);

	ImmutableArray<NonContinuousValueDescription> IMonitorInputSelectFeature.InputSources => _inputSources;

	ValueTask<ushort> IMonitorInputSelectFeature.GetInputSourceAsync(CancellationToken cancellationToken)
		=> GetNonContinuousVcpAsync(SupportedFeatures.InputSelect, _inputSelectVcpCode, cancellationToken);

	ValueTask IMonitorInputSelectFeature.SetInputSourceAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.InputSelect, _validInputSources, _inputSelectVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorRedVideoGainFeature.GetRedVideoGainAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.VideoGainRed, _redVideoGainVcpCode, cancellationToken);
	ValueTask IMonitorRedVideoGainFeature.SetRedVideoGainAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.VideoGainRed, _redVideoGainVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorGreenVideoGainFeature.GetGreenVideoGainAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.VideoGainGreen, _greenVideoGainVcpCode, cancellationToken);
	ValueTask IMonitorGreenVideoGainFeature.SetGreenVideoGainAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.VideoGainGreen, _greenVideoGainVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorBlueVideoGainFeature.GetBlueVideoGainAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.VideoGainBlue, _blueVideoGainVcpCode, cancellationToken);
	ValueTask IMonitorBlueVideoGainFeature.SetBlueVideoGainAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.VideoGainBlue, _blueVideoGainVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorRedSixAxisSaturationControlFeature.GetRedSixAxisSaturationControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisSaturationControlRed, _redSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorRedSixAxisSaturationControlFeature.SetRedSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisSaturationControlRed, _redSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorYellowSixAxisSaturationControlFeature.GetYellowSixAxisSaturationControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisSaturationControlYellow, _yellowSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorYellowSixAxisSaturationControlFeature.SetYellowSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisSaturationControlYellow, _yellowSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorGreenSixAxisSaturationControlFeature.GetGreenSixAxisSaturationControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisSaturationControlGreen, _greenSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorGreenSixAxisSaturationControlFeature.SetGreenSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisSaturationControlGreen, _greenSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorCyanSixAxisSaturationControlFeature.GetCyanSixAxisSaturationControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisSaturationControlCyan, _cyanSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorCyanSixAxisSaturationControlFeature.SetCyanSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisSaturationControlCyan, _cyanSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorBlueSixAxisSaturationControlFeature.GetBlueSixAxisSaturationControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisSaturationControlBlue, _blueSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorBlueSixAxisSaturationControlFeature.SetBlueSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisSaturationControlBlue, _blueSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorMagentaSixAxisSaturationControlFeature.GetMagentaSixAxisSaturationControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisSaturationControlMagenta, _magentaSixAxisSaturationControlVcpCode, cancellationToken);
	ValueTask IMonitorMagentaSixAxisSaturationControlFeature.SetMagentaSixAxisSaturationControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisSaturationControlMagenta, _magentaSixAxisSaturationControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorRedSixAxisHueControlFeature.GetRedSixAxisHueControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisHueControlRed, _redSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorRedSixAxisHueControlFeature.SetRedSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisHueControlRed, _redSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorYellowSixAxisHueControlFeature.GetYellowSixAxisHueControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisHueControlYellow, _yellowSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorYellowSixAxisHueControlFeature.SetYellowSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisHueControlYellow, _yellowSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorGreenSixAxisHueControlFeature.GetGreenSixAxisHueControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisHueControlGreen, _greenSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorGreenSixAxisHueControlFeature.SetGreenSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisHueControlGreen, _greenSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorCyanSixAxisHueControlFeature.GetCyanSixAxisHueControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisHueControlCyan, _cyanSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorCyanSixAxisHueControlFeature.SetCyanSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisHueControlCyan, _cyanSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorBlueSixAxisHueControlFeature.GetBlueSixAxisHueControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisHueControlBlue, _blueSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorBlueSixAxisHueControlFeature.SetBlueSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisHueControlBlue, _blueSixAxisHueControlVcpCode, value, cancellationToken);

	ValueTask<ContinuousValue> IMonitorMagentaSixAxisHueControlFeature.GetMagentaSixAxisHueControlAsync(CancellationToken cancellationToken)
		=> GetVcpAsync(SupportedFeatures.SixAxisHueControlMagenta, _magentaSixAxisHueControlVcpCode, cancellationToken);
	ValueTask IMonitorMagentaSixAxisHueControlFeature.SetMagentaSixAxisHueControlAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.SixAxisHueControlMagenta, _magentaSixAxisHueControlVcpCode, value, cancellationToken);

	ImmutableArray<NonContinuousValueDescription> IMonitorOsdLanguageFeature.Languages => _osdLanguages;

	ValueTask<ushort> IMonitorOsdLanguageFeature.GetOsdLanguageAsync(CancellationToken cancellationToken)
		=> GetNonContinuousVcpAsync(SupportedFeatures.OsdLanguage, _osdLanguageVcpCode, cancellationToken);

	ValueTask IMonitorOsdLanguageFeature.SetOsdLanguageAsync(ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(SupportedFeatures.OsdLanguage, _validOsdLanguages, _osdLanguageVcpCode, value, cancellationToken);
}
