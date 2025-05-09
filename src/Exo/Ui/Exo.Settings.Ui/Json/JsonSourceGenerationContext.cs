using System.Text.Json.Serialization;
using Exo.Settings.Ui.Models;

namespace Exo.Settings.Ui.Json;

[JsonSourceGenerationOptions(
	WriteIndented = false,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LightingConfiguration))]
[JsonSerializable(typeof(DeviceLightingConfiguration))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
