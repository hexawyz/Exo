using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>A breathing effect supporting multiple colors.</summary>
[TypeId(0x6D01AB73, 0x37AF, 0x4A1A, 0x8F, 0xF4, 0xEF, 0xC3, 0x94, 0x0F, 0x7B, 0xF9)]
public readonly partial struct VariableMultiColorBreathingEffect(in FixedList8<RgbColor> colors, PredeterminedEffectSpeed speed) : ILightingEffect
{
	[Display(Name = "Colors")]
	[Array(1, 8)]
	[DefaultValue("#51007A,")]
	public readonly FixedList8<RgbColor> Colors = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public readonly PredeterminedEffectSpeed Speed = speed;
}
