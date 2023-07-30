using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public enum WatchNotificationKind
{
	[EnumMember]
	Enumeration = 0,
	[EnumMember]
	Arrival = 1,
	[EnumMember]
	Removal = 2,
	[EnumMember]
	Update = 3,
}
