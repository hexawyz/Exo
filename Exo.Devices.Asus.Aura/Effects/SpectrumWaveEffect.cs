using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0xF6A8C369, 0xD230, 0x4E63, 0xB6, 0x00, 0xA4, 0x4F, 0x1B, 0x3B, 0xBE, 0xCA)]
public readonly struct SpectrumWaveEffect : ILightingEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; }

	[DataMember(Order = 2)]
	[Display(Name = "Reverse")]
	public bool IsReversed { get; }

	public SpectrumWaveEffect(PredeterminedEffectSpeed speed, bool isReversed)
	{
		Speed = speed;
		IsReversed = isReversed;
	}
}
