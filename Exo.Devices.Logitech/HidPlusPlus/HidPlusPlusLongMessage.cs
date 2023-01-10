using System.Runtime.InteropServices;

namespace Exo.Devices.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Explicit, Size = 20)]
public struct HidPlusPlusLongMessage<TParameters>
	where TParameters : struct, IMessageParameters
{
	[FieldOffset(0)]
	public HidPlusPlusHeader Header;
	[FieldOffset(4)]
	public TParameters Parameters;
}
