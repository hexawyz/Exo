using Exo.Lighting.Effects;

namespace Exo.Devices.Razer.LightingEffects;

/// <summary>For dock devices, represents the lighting effect where the lighting is synchronized with the mouse.</summary>
[TypeId(0x7B58F6D8, 0xE492, 0x452C, 0xA7, 0x47, 0x43, 0xD1, 0xA9, 0xC4, 0x61, 0x77)]
public readonly partial struct SynchronizedEffect : ISingletonLightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	public static ISingletonLightingEffect SharedInstance { get; } = new SynchronizedEffect();
}
