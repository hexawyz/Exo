using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents a color effect similar to the marquee effect, but much wider and with speed slowing down.</summary>
[TypeId(0x40E1DD95, 0x6DE2, 0x40C3, 0xA8, 0x47, 0xE9, 0x27, 0xBF, 0xBB, 0x34, 0x1C)]
public readonly partial struct ReversibleVariableColorLoadingEffect(RgbColor color, PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Color")]
	[DefaultValue("#51007A")]
	public RgbColor Color { get; } = color;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
}
