using Microsoft.Extensions.Logging;

namespace Exo.Devices.Logitech;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 5101, Level = LogLevel.Error, Message = "An exception occurred in the BatteryChanged handler.")]
	public static partial void LogitechUniversalDriverBatteryStateChangedError(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 5102, Level = LogLevel.Error, Message = "An exception occurred in the LockKeysChanged handler.")]
	public static partial void LogitechUniversalDriverLockKeysChangedError(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 5103, Level = LogLevel.Error, Message = "An exception occurred in the BacklightStateChanged handler.")]
	public static partial void LogitechUniversalDriverBacklightStateChangedError(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 5104, Level = LogLevel.Error, Message = "An exception occurred in the ProfileChanged handler.")]
	public static partial void LogitechUniversalDriverProfileChangedError(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 5105, Level = LogLevel.Error, Message = "An exception occurred in the DpiChanged handler.")]
	public static partial void LogitechUniversalDriverDpiChangedError(this ILogger logger, Exception exception);
}
