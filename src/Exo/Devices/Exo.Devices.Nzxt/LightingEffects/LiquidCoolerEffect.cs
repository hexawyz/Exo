using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents a color effect split in two like a thermometer, where a brighter dot is moving from start to end.</summary>
[TypeId(0x1916B46A, 0xC93C, 0x4C38, 0xAC, 0xF5, 0xAC, 0x0A, 0x0E, 0x65, 0xA7, 0xC4)]
public readonly partial struct LiquidCoolerEffect(FixedArray2<RgbColor> colors, PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Colors")]
	[DefaultValue("#FF0000,#0000FF")]
	public FixedArray2<RgbColor> Colors { get; } = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
}
