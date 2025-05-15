using System.Text.Json.Serialization;

namespace Exo.Discovery;

[JsonSourceGenerationOptions(
	WriteIndented = false,
	NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DnsSdFactoryDetails))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
