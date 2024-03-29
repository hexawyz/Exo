﻿using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
internal readonly struct RawVeryLongMessage
{
	public readonly RawMessageHeader Header;
	public readonly RawVeryLongMessageParameters Parameters;
}
