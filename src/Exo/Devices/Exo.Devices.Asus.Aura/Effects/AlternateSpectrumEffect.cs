using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0xEDD773B0, 0x727E, 0x4C12, 0xB9, 0x2A, 0xDA, 0x05, 0x5A, 0xCE, 0x49, 0x91)]
public readonly struct AlternateSpectrumEffect : ILightingEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; }

	public AlternateSpectrumEffect(PredeterminedEffectSpeed speed)
	{
		Speed = speed;
	}
}
