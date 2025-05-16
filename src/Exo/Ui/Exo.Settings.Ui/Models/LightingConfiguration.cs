using System.Text.Json.Serialization;
using Exo.Lighting;

namespace Exo.Settings.Ui.Models;

[method: JsonConstructor]
internal readonly struct LightingConfiguration(
	bool useCentralizedLighting,
	LightingEffect centralizedLightingEffect,
	Dictionary<Guid, DeviceLightingConfiguration> devices)
{
	public bool UseCentralizedLighting { get; } = useCentralizedLighting;
	public LightingEffect CentralizedLightingEffect { get; } = centralizedLightingEffect;
	public Dictionary<Guid, DeviceLightingConfiguration> Devices { get; } = devices;
}
