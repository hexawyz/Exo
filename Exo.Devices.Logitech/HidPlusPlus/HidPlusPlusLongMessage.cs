using System.Runtime.InteropServices;

namespace Exo.Devices.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Sequential, Pack = 0, Size = 20)]
public struct HidPlusPlusLongMessage<TParameters>
	where TParameters : struct, IMessageParameters
{
	public HidPlusPlusHeader Header;
	public TParameters Parameters;
}
