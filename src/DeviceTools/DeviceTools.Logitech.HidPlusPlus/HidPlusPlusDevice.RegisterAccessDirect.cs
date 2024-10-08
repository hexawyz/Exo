using System.Runtime.CompilerServices;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using Microsoft.Extensions.Logging;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public sealed class RegisterAccessDirect : RegisterAccess
	{
		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		internal RegisterAccessDirect(HidPlusPlusTransport transport, ILogger<RegisterAccessDirect> logger, HidPlusPlusDeviceId[] deviceIds, byte mainDeviceIdIndex, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, logger, deviceIds, mainDeviceIdIndex, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}

		public override ValueTask DisposeAsync(bool parentDisposed) => Transport.DisposeAsync();
	}
}
