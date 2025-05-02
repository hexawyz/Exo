using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents a color effect that behaves like a candle flame.</summary>
[TypeId(0x9EE4B83B, 0xFB32, 0x4B05, 0x97, 0x0A, 0xE8, 0xF0, 0x39, 0xA5, 0x1B, 0x6E)]
public readonly partial struct CandleEffect(RgbColor color) : ISingleColorLightEffect
{
	[Display(Name = "Color")]
	[DefaultValue("#FFA500")]
	public RgbColor Color { get; } = color;
}
