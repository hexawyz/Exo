using Microsoft.Extensions.Logging;

namespace Exo.Devices.Monitors;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Information, Message = "Capabilities for the monitor {MonitorId} were retrieved: {Capabilities}.")]
	public static partial void MonitorRetrievedCapabilities(this ILogger logger, string monitorId, string capabilities);

}
