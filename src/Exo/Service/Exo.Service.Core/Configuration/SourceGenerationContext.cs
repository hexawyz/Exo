using System.Text.Json.Serialization;
using Exo.Lighting;

namespace Exo.Service.Configuration;

[JsonSourceGenerationOptions(
	WriteIndented = false,
	Converters = [typeof(JsonStringEnumConverter)],
	NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PersistedLightingDeviceInformation))]
[JsonSerializable(typeof(PersistedLightingZoneInformation))]
[JsonSerializable(typeof(PersistedLightingDeviceConfiguration))]
[JsonSerializable(typeof(LightingEffect))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
