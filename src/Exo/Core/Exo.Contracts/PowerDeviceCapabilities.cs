using System.Runtime.Serialization;

namespace Exo.Contracts;

[Flags]
[DataContract]
public enum PowerDeviceCapabilities : byte
{
	[EnumMember]
	None = 0x00,
	[EnumMember]
	HasBattery = 0x01,
	[EnumMember]
	HasLowPowerBatteryThreshold = 0x02,
	[EnumMember]
	HasIdleTimer = 0x04,
	[EnumMember]
	HasWirelessBrightness = 0x08,
}
