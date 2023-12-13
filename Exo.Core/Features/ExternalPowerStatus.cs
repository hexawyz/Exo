using System.Runtime.Serialization;

namespace Exo.Features;

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
