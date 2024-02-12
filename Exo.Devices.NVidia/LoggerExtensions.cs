using Microsoft.Extensions.Logging;

namespace Exo.Devices.NVidia;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 10001, EventName = "NvApiVersion", Level = LogLevel.Information, Message = "NVAPI Version is {NvApiVersion}.")]
	public static partial void NvApiVersion(this ILogger logger, string nvApiVersion);
}
