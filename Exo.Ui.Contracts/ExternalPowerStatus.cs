using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public enum ExternalPowerStatus : byte
{
	[EnumMember]
	IsDisconnected = 0,
	[EnumMember]
	IsConnected = 1,
	[EnumMember]
	IsSlowCharger = 2,
}
