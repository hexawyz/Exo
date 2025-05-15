using System.Collections.Immutable;
using System.Text.Json.Serialization;
using DeviceTools;
using Exo.Images;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Service.Configuration;

[JsonSourceGenerationOptions(
	WriteIndented = false,
	Converters =
	[
		typeof(JsonStringEnumConverter<DeviceIdSource>),
		typeof(JsonStringEnumConverter<VendorIdSource>),
		typeof(JsonStringEnumConverter<DeviceCategory>),
		typeof(JsonStringEnumConverter<LightingPersistenceMode>),
		typeof(JsonStringEnumConverter<LightingDataType>),
		typeof(JsonStringEnumConverter<EffectDirection1D>),
		typeof(JsonStringEnumConverter<ImageFormat>),
	],
	NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeviceInformation))]
[JsonSerializable(typeof(IndexedConfigurationKey))]
[JsonSerializable(typeof(DeviceUserConfiguration))]
[JsonSerializable(typeof(KeyValuePair<UInt128, ImageMetadata>))]
[JsonSerializable(typeof(PersistedLightingDeviceInformation))]
[JsonSerializable(typeof(PersistedLightingZoneInformation))]
[JsonSerializable(typeof(PersistedLightingConfiguration))]
[JsonSerializable(typeof(PersistedLightingDeviceConfiguration))]
[JsonSerializable(typeof(LightingEffect))]
[JsonSerializable(typeof(PersistedLightingEffectInformation))]
[JsonSerializable(typeof(ConfigurablePropertyInformation))]
[JsonSerializable(typeof(LightingDataType))]
[JsonSerializable(typeof(EffectDirection1D))]
[JsonSerializable(typeof(ImmutableArray<EnumerationValue>))]
[JsonSerializable(typeof(EnumerationValue))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
