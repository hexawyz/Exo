using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a chasing color effect, for a zone that has more than one light.</summary>
[TypeId(0xA4FBC975, 0x3CBF, 0x48AA, 0x9B, 0xFB, 0x1F, 0x12, 0x89, 0xE7, 0xC3, 0xA0)]
public readonly struct VariableColorChaseEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	[DataMember(Order = 2)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; }

	public VariableColorChaseEffect(RgbColor color, PredeterminedEffectSpeed speed)
	{
		Color = color;
		Speed = speed;
	}
}
