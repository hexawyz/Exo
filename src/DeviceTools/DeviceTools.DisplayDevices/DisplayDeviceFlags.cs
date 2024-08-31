using System;

namespace DeviceTools.DisplayDevices
{
	[Flags]
	internal enum DisplayDeviceFlags
	{
		AttachedToDesktop = 0x00000001,
		MultiDriver = 0x00000002,
		PrimaryDevice = 0x00000004,
		MirroringDriverV = 0x00000008,
		VgaCompatible = 0x00000010,
		Removable = 0x00000020,
		AccDriver = 0x00000040,
		ModesPruned = 0x08000000,
		RdpUserModeDeviceDriver = 0x01000000, // RDPUDD…
		Remote = 0x04000000,
		Disconnect = 0x02000000,
		TsComaptible = 0x00200000,
		UnsafeModesOn = 0x00080000,
		Active = 0x00000001,
		Attached = 0x00000002,
	}
}
