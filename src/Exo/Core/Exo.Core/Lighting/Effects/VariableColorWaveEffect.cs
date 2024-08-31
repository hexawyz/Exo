using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where a color will move across an area following a wave pattern.</summary>
[TypeId(0x3798FD63, 0x6B69, 0x4167, 0xAD, 0x09, 0xB0, 0x06, 0x20, 0x82, 0x17, 0x8C)]
public readonly struct VariableColorWaveEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	[DataMember(Order = 2)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; }

	public VariableColorWaveEffect(RgbColor color, PredeterminedEffectSpeed speed)
	{
		Color = color;
		Speed = speed;
	}
}
