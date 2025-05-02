using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a flashing color effect.</summary>
[TypeId(0xF786DD5A, 0x83A7, 0x4B07, 0x91, 0x66, 0xD2, 0x31, 0xEF, 0x2D, 0xDE, 0x11)]
public readonly partial struct VariableColorFlashEffect(RgbColor color, PredeterminedEffectSpeed speed) : ISingleColorLightEffect
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
