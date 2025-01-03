using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[Flags]
[DataContract]
public enum SensorCapabilities : byte
{
	[EnumMember]
	None = 0b00000000,
	[EnumMember]
	Polled = 0b00000001,
	[EnumMember]
	Streamed = 0b00000010,
}
