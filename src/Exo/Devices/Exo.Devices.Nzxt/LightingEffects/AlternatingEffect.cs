using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents a color effect where two colors are alternated over the zone, either switching over time or moving from start to end.</summary>
[TypeId(0x586043B8, 0x5506, 0x48A9, 0x96, 0x89, 0xC1, 0x7F, 0x67, 0xD0, 0x40, 0xC2)]
public readonly partial struct AlternatingEffect(FixedArray2<RgbColor> colors, PredeterminedEffectSpeed speed, EffectDirection1D direction, byte size, bool isMoving) : ILightingEffect
{
	[Display(Name = "Colors")]
	[DefaultValue("#FF0000,#0000FF")]
	public FixedArray2<RgbColor> Colors { get; } = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
	[Display(Name = "Size")]
	[Range(3, 6)]
	[DefaultValue(3)]
	public byte Size { get; } = size;
	[Display(Name = "Moving")]
	public bool IsMoving { get; } = isMoving;
}
