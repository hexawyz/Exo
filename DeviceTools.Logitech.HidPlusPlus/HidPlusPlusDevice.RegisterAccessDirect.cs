using System.Runtime.CompilerServices;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public sealed class RegisterAccessDirect : RegisterAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		internal RegisterAccessDirect(HidPlusPlusTransport transport, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}

		public override ValueTask DisposeAsync() => Transport.DisposeAsync();
	}
}
