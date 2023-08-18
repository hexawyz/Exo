using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 5001, Level = LogLevel.Error, Message = "Invalid message received from the device.")]
	public static partial void UltraGearLightingTransportInvalidMessage(this ILogger logger, Exception exception);
}
