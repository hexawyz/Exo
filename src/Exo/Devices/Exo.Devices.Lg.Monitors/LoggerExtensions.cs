using Microsoft.Extensions.Logging;

namespace Exo.Devices.Lg.Monitors;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when processing an incoming message on the LG UltraGear lighting interface.")]
	public static partial void UltraGearLightingTransportMessageProcessingError(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The message had an invalid header: {header:X2}.")]
	public static partial void UltraGearLightingTransportInvalidMessageHeader(this ILogger logger, byte header);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The message had an invalid checksum. Actual: {actualChecksum:X2}. Expected: {expectedChecksum:X2}.")]
	public static partial void UltraGearLightingTransportInvalidMessageChecksum(this ILogger logger, byte actualChecksum, byte expectedChecksum);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The response message had an invalid data length.")]
	public static partial void HidI2CTransportMessageInvalidMessageDataLength(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The response message contained an invalid value.")]
	public static partial void HidI2CTransportMessageInvalidMessage(this ILogger logger);
}
