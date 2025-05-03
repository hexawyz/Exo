using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

[TypeId(0xA2AC5968, 0xEBE6, 0x4007, 0xBE, 0x24, 0x68, 0x1A, 0x77, 0xD9, 0xCA, 0xAF)]
public readonly partial struct ReversibleVariableSuperRainbowEffect(PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
}
