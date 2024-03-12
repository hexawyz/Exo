using Microsoft.Extensions.Logging;

namespace Exo.Devices.Gigabyte;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Failed to access the SMBus driver.")]
	public static partial void AcpiSystemManagementBusAccessError(this ILogger logger, Exception exception);

}
