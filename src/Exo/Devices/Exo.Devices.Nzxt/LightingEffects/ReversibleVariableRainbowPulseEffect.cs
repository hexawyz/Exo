using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

[TypeId(0xF26B97AB, 0x38EE, 0x4F7D, 0x93, 0x18, 0x0C, 0x67, 0xFF, 0xC1, 0x66, 0x57)]
public readonly partial struct ReversibleVariableRainbowPulseEffect(PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
}
