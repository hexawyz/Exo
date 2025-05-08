using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0x36DE8ED5, 0x1783, 0x4EFC, 0xB3, 0x1A, 0xA9, 0xB0, 0xB6, 0x56, 0xB0, 0x9A)]
public readonly partial struct SpectrumCycleWaveEffect(PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
	[Display(Name = "Direction")]
	public EffectDirection1D Direction { get; } = direction;
}
