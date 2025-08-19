using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0x17325DE9, 0x9572, 0x41C7, 0xA1, 0xE7, 0x8D, 0x1B, 0x2A, 0x28, 0xEA, 0x30)]
public readonly partial struct WideSpectrumCycleChaseEffect(PredeterminedEffectSpeed speed) : ILightingEffect
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
