using System.Text.Json.Serialization;
using DeviceTools;

namespace Exo.Discovery;

[JsonSourceGenerationOptions(
	WriteIndented = false,
	Converters =
	[
		typeof(JsonStringEnumConverter<VendorIdSource>),
	],
	NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HidFactoryDetails))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
