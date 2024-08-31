using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum LightingPersistenceMode : byte
{
	[EnumMember]
	NeverPersisted = 0,
	[EnumMember]
	CanPersist = 1,
	[EnumMember]
	AlwaysPersisted = 2,
}
