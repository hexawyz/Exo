using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

/// <summary>Represents a chasing color effect, for a zone that has more than one light.</summary>
[TypeId(0xBAB1B0F1, 0x55F3, 0x46F8, 0x9A, 0xDC, 0xE6, 0x98, 0x4B, 0x38, 0x19, 0x02)]
public readonly partial struct ColorChaseEffect(RgbColor color, PredeterminedEffectSpeed speed, EffectDirection1D direction) : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;

	[DataMember(Order = 2)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
}
