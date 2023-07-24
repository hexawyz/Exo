using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public enum DeviceNotificationKind
{
	Enumeration = 0,
	Arrival = 1,
	Removal = 2,
	Update = 3,
}
