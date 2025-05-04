using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer.LightingEffects;

/// <summary>Represents a light with a breathing color effect of one or two colors.</summary>
[TypeId(0x45E154D6, 0x1946, 0x4215, 0xA4, 0x4F, 0x97, 0x5C, 0xB7, 0x6D, 0xEE, 0xAE)]
public readonly partial struct MultiColorBreathingEffect(in FixedList2<RgbColor> colors) : ILightingEffect
{
	[Display(Name = "Colors")]
	[Array(1, 2)]
	[DefaultValue("#00FF00,")]
	public readonly FixedList2<RgbColor> Colors = colors;
}
