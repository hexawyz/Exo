using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
[Flags]
public enum EmbeddedMonitorCapabilities : uint
{
	[EnumMember]
	None = 0b00000000,
	[EnumMember]
	StaticImages = 0b00000001,
	[EnumMember]
	AnimatedImages = 0b00000010,
	[EnumMember]
	PartialUpdates = 0b00000100,
	[EnumMember]
	ScreensaverImage = 0b00001000,
}
