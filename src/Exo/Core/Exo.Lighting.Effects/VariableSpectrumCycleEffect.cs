using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where colors of the rainbow will move in a wave.</summary>
[TypeId(0x712094B5, 0xC5B9, 0x4A2B, 0x96, 0x19, 0xE3, 0x3F, 0xB0, 0x49, 0xEE, 0x9F)]
public readonly partial struct VariableSpectrumCycleEffect(PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<SpectrumCycleEffect, VariableSpectrumCycleEffect>
{
	[DataMember(Order = 1)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	public static implicit operator VariableSpectrumCycleEffect(in SpectrumCycleEffect effect)
		=> new(PredeterminedEffectSpeed.MediumFast);
}
