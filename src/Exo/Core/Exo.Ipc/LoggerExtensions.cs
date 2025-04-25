using Microsoft.Extensions.Logging;

namespace Exo.Ipc;

internal static partial class LoggerExtensions
{
	[LoggerMessage(LogLevel.Error, "An error occurred while processing pipe messages.")]
	public static partial void PipeConnectionReadError(this ILogger logger, Exception exception);
}
