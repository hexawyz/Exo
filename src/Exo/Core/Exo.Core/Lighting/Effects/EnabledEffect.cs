using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a color effect that has fixed parameters.</summary>
[TypeId(0x78BF8AE8, 0xF08E, 0x44B5, 0xB8, 0xFB, 0xE9, 0xDE, 0xE9, 0x26, 0x30, 0xF4)]
public readonly partial struct EnabledEffect : ISingletonLightingEffect
{
	public static ISingletonLightingEffect SharedInstance { get; } = new EnabledEffect();
}
