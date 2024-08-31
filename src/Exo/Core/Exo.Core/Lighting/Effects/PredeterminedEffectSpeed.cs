using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

[DataContract]
public enum PredeterminedEffectSpeed : byte
{
	[EnumMember]
	[Display(Name = "Slower")]
	Slower = 0,
	[EnumMember]
	[Display(Name = "Slow")]
	Slow = 1,
	[EnumMember]
	[Display(Name = "Medium Slow")]
	MediumSlow = 2,
	[EnumMember]
	[Display(Name = "Medium Fast")]
	MediumFast = 3,
	[EnumMember]
	[Display(Name = "Fast")]
	Fast = 4,
	[EnumMember]
	[Display(Name = "Faster")]
	Faster = 5,
}
