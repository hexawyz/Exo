using System.Runtime.InteropServices;

namespace Exo.Devices.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 7)]
internal readonly struct RawShortMessage
{
	public readonly RawMessageHeader Header;
	public readonly RawShortMessageParameters Parameters;
}
