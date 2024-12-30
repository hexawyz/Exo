using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

internal static partial class LoggerExtensions
{
	[LoggerMessage(
		EventId = 60_101,
		EventName = "KrakenWinUsbDeviceLocked",
		Level = LogLevel.Error,
		Message = "The Kraken device at {DeviceName} is locked by another software. The image upload feature will be disabled.")]
	public static partial void KrakenWinUsbDeviceLocked
	(
		this ILogger logger,
		string deviceName
	);
}
