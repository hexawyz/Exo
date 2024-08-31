using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "An error occurred when processing an incoming message on the LG UltraGear lighting interface.")]
	public static partial void UltraGearLightingTransportMessageProcessingError(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "The message had an invalid header: {header:X2}.")]
	public static partial void UltraGearLightingTransportInvalidMessageHeader(this ILogger logger, byte header);

	[LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "The message had an invalid checksum. Actual: {actualChecksum:X2}. Expected: {expectedChecksum:X2}.")]
	public static partial void UltraGearLightingTransportInvalidMessageChecksum(this ILogger logger, byte actualChecksum, byte expectedChecksum);

	[LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "The response message had an invalid data length.")]
	public static partial void HidI2CTransportMessageInvalidMessageDataLength(this ILogger logger);

	[LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "The response message contained an invalid value.")]
	public static partial void HidI2CTransportMessageInvalidMessage(this ILogger logger);
}
