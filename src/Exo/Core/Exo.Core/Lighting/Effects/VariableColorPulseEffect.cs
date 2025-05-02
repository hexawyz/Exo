using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a pulsing color effect.</summary>
[TypeId(0x433FC57B, 0x6486, 0x48EA, 0x8F, 0xA1, 0x1D, 0x2A, 0x93, 0xE1, 0x92, 0xCB)]
public readonly partial struct VariableColorPulseEffect(RgbColor color, PredeterminedEffectSpeed speed) : ISingleColorLightEffect
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
