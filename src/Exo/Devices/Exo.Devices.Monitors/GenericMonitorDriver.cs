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
		II2cBus i2cBus,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		var ddc = new DisplayDataChannel(i2cBus, true);
		var monitorId = new MonitorId(edid.VendorId, edid.ProductId);
		var featureSetBuilder = new MonitorFeatureSetBuilder();
		var info = await PrepareMonitorFeaturesAsync(logger, featureSetBuilder, ddc, monitorId, cancellationToken).ConfigureAwait(false);

		if (info.Definition.Name is not null) friendlyName = info.Definition.Name;

		if (friendlyName is null && info.Capabilities is not null) friendlyName = info.Capabilities.Model;

		if (friendlyName is null) throw new InvalidOperationException("No friendly name for the monitor.");

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
									int index = value.Value - 1;
									// In principle, the language name strings should all be defined in the global strings, so that they can be reused across different needs.
									nameId = (uint)index < (uint)LanguageIds.Count ? LanguageIds[index] : default;
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
