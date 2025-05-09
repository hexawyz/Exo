using System.Text.Json.Serialization;

namespace Exo.Settings.Ui.Models;

[method: JsonConstructor]
internal readonly struct LightingConfiguration(
	bool useGlobalLighting,
	Dictionary<Guid, DeviceLightingConfiguration> devices)
{
	// TODO: Implement a global lighting mode. The way I see it, it would be saved as a separate configuration in the lighting service.
	// Once the lighting converters are wired in properly, it should be easy to finally get this feature for setting the lighting to a single color globally.
	// Ideally, even some common effects like pulse/breathing, which I think is doable using something like an effect priority list, which would allow appropriate fallbacks to simpler effects as needed.
	public bool UseGlobalLighting { get; } = useGlobalLighting;
	//public GlobalLightingConfiguration? Global { get; }
	public Dictionary<Guid, DeviceLightingConfiguration> Devices { get; } = devices;
}
