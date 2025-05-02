using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where colors of the rainbow will move in a wave.</summary>
[TypeId(0xD11B8022, 0x2C92, 0x467A, 0xB8, 0x63, 0x9B, 0x70, 0x3D, 0x26, 0x5A, 0x70)]
public readonly partial struct VariableSpectrumWaveEffect(PredeterminedEffectSpeed speed) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
