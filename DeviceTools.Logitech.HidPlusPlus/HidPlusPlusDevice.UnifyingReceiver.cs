using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public sealed class UnifyingReceiver : RegisterAccessReceiver
	{
		internal UnifyingReceiver(HidPlusPlusTransport transport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}
	}
}
