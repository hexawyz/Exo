using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0x716E4BB3, 0x6725, 0x4D98, 0x86, 0x3B, 0xE8, 0xDD, 0xE8, 0xA7, 0x87, 0xB6)]
public readonly partial struct SpectrumCyclePulseEffect(PredeterminedEffectSpeed speed) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
