using System.Text.Json.Serialization;
using Exo.Lighting;

namespace Exo.Settings.Ui.Models;

[method: JsonConstructor]
internal readonly struct DeviceLightingConfiguration(
	byte? brightness,
	bool useUnifiedLighting,
	Dictionary<Guid, LightingEffect> zones)
{
	public byte? Brightness { get; } = brightness;
	//public LightingPalette? Palette { get; }
	public bool UseUnifiedLighting { get; } = useUnifiedLighting;
	public Dictionary<Guid, LightingEffect> Zones { get; } = zones;
}
