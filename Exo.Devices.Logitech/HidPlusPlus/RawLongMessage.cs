﻿using System.Runtime.InteropServices;

namespace Exo.Devices.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
internal readonly struct RawLongMessage
{
	public readonly RawMessageHeader Header;
	public readonly RawLongMessageParameters Parameters;
}
