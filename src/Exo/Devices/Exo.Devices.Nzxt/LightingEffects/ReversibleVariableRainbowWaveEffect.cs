using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

[TypeId(0xEFC68BAA, 0x98AE, 0x4123, 0xA6, 0xFA, 0x56, 0xA6, 0xBC, 0x20, 0x28, 0x78)]
public readonly partial struct ReversibleVariableRainbowWaveEffect(PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
}
