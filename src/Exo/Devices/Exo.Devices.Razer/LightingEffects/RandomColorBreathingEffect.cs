using Exo.Lighting.Effects;

namespace Exo.Devices.Razer.LightingEffects;

/// <summary>Represents a light with a pulsing color effect alternating between random colors.</summary>
[TypeId(0xB7CE4E5E, 0x4983, 0x4D3B, 0xA0, 0xD1, 0xEE, 0xB2, 0x28, 0x28, 0x37, 0x76)]
public readonly partial struct RandomColorBreathingEffect : ISingletonLightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	public static ISingletonLightingEffect SharedInstance { get; } = new RandomColorBreathingEffect();
}
