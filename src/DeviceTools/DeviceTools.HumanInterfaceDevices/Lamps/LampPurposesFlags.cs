using System;

namespace DeviceTools.HumanInterfaceDevices.Lamps
{
	[Flags]
	public enum LampPurposesFlags : uint
	{
		None = 0x00,
		LampPurposeControl = 0x01,
		LampPurposeAccent = 0x02,
		LampPurposeBranding = 0x04,
		LampPurposeStatus = 0x08,
		LampPurposeIllumination = 0x10,
		LampPurposePresentation = 0x20,
	}
}
