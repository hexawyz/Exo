using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0x1FA781E9, 0x2426, 0x4F06, 0x9B, 0x6E, 0x72, 0x55, 0xEE, 0x02, 0xA4, 0x3A)]
public readonly partial struct SparklingSpectrumCycleEffect(PredeterminedEffectSpeed speed) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
