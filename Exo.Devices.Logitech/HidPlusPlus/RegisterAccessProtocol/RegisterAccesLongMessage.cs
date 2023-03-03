using System.Runtime.InteropServices;
using Exo.Devices.Logitech.HidPlusPlus.FeatureAccessProtocol;

namespace Exo.Devices.Logitech.HidPlusPlus.RegisterAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct RegisterAccesLongMessage<TParameters>
	where TParameters : struct, ILongMessageParameters
{
	public FeatureAccessHeader Header;
	public TParameters Parameters;
}
