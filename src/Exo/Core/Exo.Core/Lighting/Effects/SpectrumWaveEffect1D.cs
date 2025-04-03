using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

[DataContract]
[TypeId(0xB3B1B3B2, 0x5616, 0x453C, 0xA3, 0x81, 0x3D, 0x87, 0xA3, 0x05, 0x15, 0xA0)]
public readonly partial struct SpectrumWaveEffect1D : ILightingEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Direction")]
	public EffectDirection1D Direction { get; }

	public SpectrumWaveEffect1D(EffectDirection1D direction)
	{
		Direction = direction;
	}
}
