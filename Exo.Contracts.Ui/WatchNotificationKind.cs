using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public enum WatchNotificationKind
{
	[EnumMember]
	Enumeration = 0,
	[EnumMember]
	Addition = 1,
	[EnumMember]
	Removal = 2,
	[EnumMember]
	Update = 3,
}
