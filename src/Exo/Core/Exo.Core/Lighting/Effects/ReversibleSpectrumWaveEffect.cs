using System.ComponentModel.DataAnnotations;

namespace Exo.Lighting.Effects;

[TypeId(0xB3B1B3B2, 0x5616, 0x453C, 0xA3, 0x81, 0x3D, 0x87, 0xA3, 0x05, 0x15, 0xA0)]
public readonly partial struct ReversibleSpectrumWaveEffect(EffectDirection1D direction) :
	ILightingEffect,
	IConvertibleLightingEffect<SpectrumWaveEffect, ReversibleSpectrumWaveEffect>
{
	[Display(Name = "Direction")]
	public EffectDirection1D Direction { get; } = direction;

	public static implicit operator ReversibleSpectrumWaveEffect(in SpectrumWaveEffect effect)
		=> new(EffectDirection1D.Forward);
}
