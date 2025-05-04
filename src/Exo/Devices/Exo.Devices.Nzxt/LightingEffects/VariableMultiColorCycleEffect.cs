using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>A color cycling effect supporting multiple colors.</summary>
[TypeId(0xA66FCE58, 0x4256, 0x4746, 0x8A, 0x86, 0x65, 0x33, 0xF6, 0xAE, 0x3E, 0xFB)]
public readonly partial struct VariableMultiColorCycleEffect(in FixedList8<RgbColor> colors, PredeterminedEffectSpeed speed) : ILightingEffect
{
	[Display(Name = "Colors")]
	[Array(2, 8)]
	[DefaultValue("#51007A,#0000FF,#00FF00")]
	public readonly FixedList8<RgbColor> Colors = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public readonly PredeterminedEffectSpeed Speed = speed;
}
