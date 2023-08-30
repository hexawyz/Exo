using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using Microsoft.Extensions.Logging;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public sealed class UnifyingReceiver : RegisterAccessReceiver
	{
		internal UnifyingReceiver
		(
			HidPlusPlusTransport transport,
			ILoggerFactory loggerFactory,
			ILogger<UnifyingReceiver> logger,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			string? friendlyName,
			string? serialNumber
		)
			: base(transport, loggerFactory, logger, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
		}
	}
}
