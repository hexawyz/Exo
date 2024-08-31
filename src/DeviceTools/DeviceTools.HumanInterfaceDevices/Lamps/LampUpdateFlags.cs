using System;

namespace DeviceTools.HumanInterfaceDevices.Lamps
{
	[Flags]
	public enum LampUpdateFlags : ushort
	{
		None = 0x00,
		LampUpdateComplete = 0x01,
	}
}
