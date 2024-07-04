using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
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

public partial class GenericMonitorDriver
	: Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	IMonitorCapabilitiesFeature,
	IMonitorRawCapabilitiesFeature,
	IMonitorRawVcpFeature
{
	private static readonly ExoArchive MonitorDefinitionsDatabase = new((UnmanagedMemoryStream)typeof(GenericMonitorDriver).Assembly.GetManifestResourceStream("Definitions.xoa")!);

	private static readonly Guid OnStringId = new(0x4D2B3404, 0x1CB1, 0x4536, 0x91, 0x8C, 0x80, 0xFA, 0xCC, 0x12, 0x4C, 0xF9);
	private static readonly Guid OffStringId = new(0xA9F9A2E6, 0x2091, 0x4BD9, 0xB1, 0x35, 0xA4, 0xA5, 0xD6, 0xD4, 0x00, 0x9E);

	protected static bool TryGetMonitorDefinition(MonitorId deviceId, out MonitorDefinition definition)
	{
		Span<byte> key = stackalloc byte[4];
		BinaryPrimitives.WriteUInt16LittleEndian(key, deviceId.VendorId.Value);
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
		var ddc = new DisplayDataChannel(i2cBus, true);
		var monitorId = new MonitorId(edid.VendorId, edid.ProductId);
		var featureSetBuilder = new MonitorFeatureSetBuilder();
		var info = await PrepareMonitorFeaturesAsync(logger, featureSetBuilder, ddc, monitorId, cancellationToken).ConfigureAwait(false);

		if (info.Definition.Name is not null) friendlyName = info.Definition.Name;

		return new DriverCreationResult<SystemDevicePath>
		(
			keys,
			new GenericMonitorDriver
			(
				ddc,
				featureSetBuilder,
				info.RawCapabilities.AsMemory(),
				info.Capabilities,
				deviceId,
				friendlyName,
				new("monitor", topLevelDeviceName, deviceId.ToString(), edid.SerialNumber)
			)
		);
	}

	protected readonly struct ConsolidatedMonitorInformation
	{
		public required ImmutableArray<byte> RawCapabilities { get; init; }
		public required MonitorCapabilities? Capabilities { get; init; }
		public required MonitorDefinition Definition { get; init; }
	}

	protected static void LogRetrievedCapabilities(ILogger logger, MonitorId monitorId, ImmutableArray<byte> rawCapabilities)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			logger.MonitorRetrievedCapabilities(monitorId.ToString()!, Encoding.UTF8.GetString(rawCapabilities.AsSpan()));
		}
	}

	/// <summary>Applies the standard setup procedure to retrieve monitor information and configure monitor features.</summary>
	/// <remarks>
	/// Calling this method is the simplest way to implement a factory for a custom driver based on <see cref="GenericMonitorDriver"/>.
	/// In cases where this method is not granular enough, one of the other methods with the proper degree of granularity can be called.
	/// </remarks>
	/// <param name="logger">The logger.</param>
	/// <param name="builder">The feature set builder on which all features will be configured.</param>
	/// <param name="ddc">A DDC instance used to fetch monitor capabilities.</param>
	/// <param name="monitorId">The monitor ID, used to fetch a custom monitor definition.</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	protected static async ValueTask<ConsolidatedMonitorInformation> PrepareMonitorFeaturesAsync
	(
		ILogger logger,
		MonitorFeatureSetBuilder builder,
		DisplayDataChannel ddc,
		MonitorId monitorId,
		CancellationToken cancellationToken
	)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(1000);
		ImmutableArray<byte> rawCapabilities;
		try
		{
			ushort length = await ddc.GetCapabilitiesAsync(buffer, cancellationToken).ConfigureAwait(false);
			rawCapabilities = [.. buffer[..length]];
			LogRetrievedCapabilities(logger, monitorId, rawCapabilities);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}

		return PrepareMonitorFeatures(builder, rawCapabilities, monitorId);
	}

	protected static ConsolidatedMonitorInformation PrepareMonitorFeatures(MonitorFeatureSetBuilder builder, ImmutableArray<byte> rawCapabilities, MonitorId monitorId)
	{
		if (TryGetMonitorDefinition(monitorId, out var definition))
		{
			// NB: We do completely override the capabilities string if a value is provided.
			// This can be a simpler way of defining the capabilities of a monitor. e.g. if it doesn't provide a capabilities string, or if the built-in one is broken.
			if (!definition.Capabilities.IsDefault) rawCapabilities = definition.Capabilities;
		}

		return PrepareMonitorFeatures(builder, rawCapabilities, definition);
	}

	protected static ConsolidatedMonitorInformation PrepareMonitorFeatures(MonitorFeatureSetBuilder builder, ImmutableArray<byte> rawCapabilities, MonitorDefinition definition)
	{
		if (MonitorCapabilities.TryParse(rawCapabilities.AsSpan(), out var capabilities))
		{
			builder.AddCapabilitiesFeature();
		}

		ConfigureFeatureSet(builder, capabilities, definition);

		return new() { RawCapabilities = rawCapabilities, Capabilities = capabilities, Definition = definition };
	}

	/// <summary>Configures the feature set for the monitor using capabilities and custom monitor definition.</summary>
	/// <remarks>
	/// <para>
	/// This is the core operation for building monitor features based on the default supported set.
	/// </para>
	/// <para>
	/// Depending on the level of customization desired, callers may want to call one of
	/// <see cref="PrepareMonitorFeaturesAsync(ILogger{GenericMonitorDriver}, MonitorFeatureSetBuilder, DisplayDataChannel, MonitorId, CancellationToken)"/>,
	/// <see cref="PrepareMonitorFeatures(MonitorFeatureSetBuilder, ImmutableArray{byte}, MonitorId)"/> or
	/// <see cref="PrepareMonitorFeatures(MonitorFeatureSetBuilder, ImmutableArray{byte}, MonitorDefinition)"/> instead.
	/// </para>
	/// </remarks>
	/// <param name="builder"></param>
	/// <param name="capabilities"></param>
	/// <param name="definition"></param>
	protected static void ConfigureFeatureSet(MonitorFeatureSetBuilder builder, MonitorCapabilities? capabilities, MonitorDefinition definition)
	{
		ImmutableArray<NonContinuousValueDescription>.Builder allowedValuesBuilder = ImmutableArray.CreateBuilder<NonContinuousValueDescription>();
		ushort offValue;
		ushort onValue;

		if (capabilities is not null)
		{ 
			builder.AddCapabilitiesFeature();

			if (!definition.IgnoreAllCapabilitiesVcpCodes)
			{
				var vcpCodesToIgnore = !definition.IgnoredCapabilitiesVcpCodes.IsDefaultOrEmpty ?
					new HashSet<byte>(ImmutableCollectionsMarshal.AsArray(definition.IgnoredCapabilitiesVcpCodes)!) :
					null;

				foreach (var capability in capabilities.SupportedVcpCommands)
				{
					// Ignore some VCP codes if they are specifically indicated to be ignored.
					// This can be useful if some features are not properly mapped by the monitor.
					if (vcpCodesToIgnore?.Contains(capability.VcpCode) == true) continue;
					switch (capability.VcpCode)
					{
					case (byte)VcpCode.Luminance:
						builder.AddBrightnessFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.Contrast:
						builder.AddContrastFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.Sharpness:
						builder.AddSharpnessFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.AudioSpeakerVolume:
						builder.AddAudioVolumeFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.InputSelect:
						if (!capability.NonContinuousValues.IsDefaultOrEmpty)
						{
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
								allowedValuesBuilder.Add(new(value.Value, nameId, value.Name));
							}
							builder.AddInputSelectFeature(capability.VcpCode, allowedValuesBuilder.DrainToImmutable());
						}
						break;
					case (byte)VcpCode.VideoGainRed:
						builder.AddRedVideoGainFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.VideoGainGreen:
						builder.AddGreenVideoGainFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.VideoGainBlue:
						builder.AddBlueVideoGainFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.VideoBlackLevelRed:
						builder.AddRedVideoBlackLevelFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.VideoBlackLevelGreen:
						builder.AddGreenVideoBlackLevelFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.VideoBlackLevelBlue:
						builder.AddBlueVideoBlackLevelFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisSaturationControlRed:
						builder.AddRedSixAxisSaturationControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisSaturationControlYellow:
						builder.AddYellowSixAxisSaturationControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisSaturationControlGreen:
						builder.AddGreenSixAxisSaturationControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisSaturationControlCyan:
						builder.AddCyanSixAxisSaturationControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisSaturationControlBlue:
						builder.AddBlueSixAxisSaturationControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisSaturationControlMagenta:
						builder.AddMagentaSixAxisSaturationControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisColorControlRed:
						builder.AddRedSixAxisHueControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisColorControlYellow:
						builder.AddYellowSixAxisHueControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisColorControlGreen:
						builder.AddGreenSixAxisHueControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisColorControlCyan:
						builder.AddCyanSixAxisHueControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisColorControlBlue:
						builder.AddBlueSixAxisHueControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.SixAxisColorControlMagenta:
						builder.AddMagentaSixAxisHueControlFeature(capability.VcpCode);
						break;
					case (byte)VcpCode.OsdLanguage:
						if (!capability.NonContinuousValues.IsDefaultOrEmpty)
						{
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
								allowedValuesBuilder.Add(new(value.Value, nameId, value.Name));
							}
							builder.AddOsdLanguageFeature(capability.VcpCode, allowedValuesBuilder.DrainToImmutable());
						}
						break;
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
						builder.AddBrightnessFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.Contrast:
						builder.AddContrastFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.Sharpness:
						builder.AddSharpnessFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.AudioVolume:
						builder.AddAudioVolumeFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.InputSelect:
						WriteDiscreteValues(allowedValuesBuilder, feature.DiscreteValues);
						builder.AddInputSelectFeature(feature.VcpCode, allowedValuesBuilder.DrainToImmutable());
						break;
					case MonitorFeature.VideoGainRed:
						builder.AddRedVideoGainFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.VideoGainGreen:
						builder.AddGreenVideoGainFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.VideoGainBlue:
						builder.AddBlueVideoGainFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.VideoBlackLevelRed:
						builder.AddRedVideoBlackLevelFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.VideoBlackLevelGreen:
						builder.AddGreenVideoBlackLevelFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.VideoBlackLevelBlue:
						builder.AddBlueVideoBlackLevelFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisSaturationControlRed:
						builder.AddRedSixAxisSaturationControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisSaturationControlYellow:
						builder.AddYellowSixAxisSaturationControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisSaturationControlGreen:
						builder.AddGreenSixAxisSaturationControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisSaturationControlCyan:
						builder.AddCyanSixAxisSaturationControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisSaturationControlBlue:
						builder.AddBlueSixAxisSaturationControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisSaturationControlMagenta:
						builder.AddMagentaSixAxisSaturationControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisHueControlRed:
						builder.AddRedSixAxisHueControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisHueControlYellow:
						builder.AddYellowSixAxisHueControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisHueControlGreen:
						builder.AddGreenSixAxisHueControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisHueControlCyan:
						builder.AddCyanSixAxisHueControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisHueControlBlue:
						builder.AddBlueSixAxisHueControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.SixAxisHueControlMagenta:
						builder.AddMagentaSixAxisHueControlFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.InputLag:
						WriteDiscreteValues(allowedValuesBuilder, feature.DiscreteValues);
						builder.AddInputLagFeature(feature.VcpCode, allowedValuesBuilder.DrainToImmutable());
						break;
					case MonitorFeature.ResponseTime:
						WriteDiscreteValues(allowedValuesBuilder, feature.DiscreteValues);
						builder.AddResponseTimeFeature(feature.VcpCode, allowedValuesBuilder.DrainToImmutable());
						break;
					case MonitorFeature.BlueLightFilterLevel:
						builder.AddBlueLightFilterLevelFeature(feature.VcpCode, feature.MinimumValue, feature.MaximumValue);
						break;
					case MonitorFeature.OsdLanguage:
						WriteDiscreteValues(allowedValuesBuilder, feature.DiscreteValues);
						builder.AddOsdLanguageFeature(feature.VcpCode, allowedValuesBuilder.DrainToImmutable());
						break;
					case MonitorFeature.PowerIndicator:
						// TODO: Log if there is a configuration problem.
						if (!feature.DiscreteValues.IsDefault && feature.DiscreteValues.Length == 2)
						{
							if (feature.DiscreteValues[0].NameStringId == OnStringId && feature.DiscreteValues[1].NameStringId == OffStringId)
							{
								onValue = feature.DiscreteValues[0].Value;
								offValue = feature.DiscreteValues[1].Value;
							}
							else
							{
								offValue = feature.DiscreteValues[0].Value;
								onValue = feature.DiscreteValues[1].Value;
							}
							if (offValue != onValue)
							{
								builder.AddPowerIndicatorToggleFeature(feature.VcpCode, offValue, onValue);
							}
						}
						break;
					}
				}
			}
		}
	}

	private static void WriteDiscreteValues(ImmutableArray<NonContinuousValueDescription>.Builder builder, ImmutableArray<MonitorFeatureDiscreteValueDefinition> discreteValues)
	{
		if (!discreteValues.IsDefaultOrEmpty)
		{
			foreach (var valueDefinition in discreteValues)
			{
				builder.Add(new(valueDefinition.Value, valueDefinition.NameStringId.GetValueOrDefault(), null));
			}
		}
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private readonly DisplayDataChannel _ddc;
	private readonly ReadOnlyMemory<byte> _rawCapabilities;
	private readonly MonitorCapabilities? _capabilities;
	private readonly DeviceId _deviceId;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;

	protected DisplayDataChannel DisplayDataChannel => _ddc;
	protected IDeviceFeatureSet<IGenericDeviceFeature> GenericFeatures => _genericFeatures;
	protected IDeviceFeatureSet<IMonitorDeviceFeature> MonitorFeatures => _monitorFeatures;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	DeviceId IDeviceIdFeature.DeviceId => _deviceId;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	protected GenericMonitorDriver
	(
		DisplayDataChannel ddc,
		MonitorFeatureSetBuilder featureSetBuilder,
		ReadOnlyMemory<byte> rawCapabilities,
		MonitorCapabilities? capabilities,
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	)
		: base(friendlyName, configurationKey)
	{
		_ddc = ddc;
		_rawCapabilities = rawCapabilities;
		_capabilities = capabilities;
		_deviceId = deviceId;

		_genericFeatures = CreateGenericFeatures(configurationKey);

		_monitorFeatures = featureSetBuilder.CreateFeatureSet(this);
	}

	protected virtual IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures(DeviceConfigurationKey configurationKey)
		=> configurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature>(this);

	public override ValueTask DisposeAsync() => _ddc.DisposeAsync();

	private async ValueTask<ContinuousValue> GetVcpAsync(byte code, CancellationToken cancellationToken)
	{
		var reply = await _ddc.GetVcpFeatureAsync(code, cancellationToken).ConfigureAwait(false);
		return new ContinuousValue(reply.CurrentValue, 0, reply.MaximumValue);
	}

	private async ValueTask<ushort> GetNonContinuousVcpAsync(byte code, CancellationToken cancellationToken)
	{
		var reply = await _ddc.GetVcpFeatureAsync(code, cancellationToken).ConfigureAwait(false);
		return reply.CurrentValue;
	}

	private async ValueTask<bool> GetBooleanVcpAsync(byte code, ushort onValue, CancellationToken cancellationToken)
	{
		var reply = await _ddc.GetVcpFeatureAsync(code, cancellationToken).ConfigureAwait(false);
		return reply.CurrentValue == onValue;
	}

	private async ValueTask SetVcpAsync(byte code, ushort value, CancellationToken cancellationToken)
		=> await _ddc.SetVcpFeatureAsync(code, value, cancellationToken).ConfigureAwait(false);

	private async ValueTask SetVcpAsync(HashSet<ushort>? supportedValues, byte code, ushort value, CancellationToken cancellationToken)
	{
		if (supportedValues is null || !supportedValues.Contains(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), "The specified value is not allowed by the definition.");
		}
		await _ddc.SetVcpFeatureAsync(code, value, cancellationToken).ConfigureAwait(false);
	}

	ReadOnlySpan<byte> IMonitorRawCapabilitiesFeature.RawCapabilities => _rawCapabilities.Span;

	MonitorCapabilities IMonitorCapabilitiesFeature.Capabilities => _capabilities!;

	ValueTask IMonitorRawVcpFeature.SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken)
		=> SetVcpAsync(vcpCode, value, cancellationToken);

	async ValueTask<VcpFeatureReply> IMonitorRawVcpFeature.GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
	{
		var result = await _ddc.GetVcpFeatureAsync(vcpCode, cancellationToken).ConfigureAwait(false);
		return new(result.CurrentValue, result.MaximumValue, result.IsMomentary);
	}
}
