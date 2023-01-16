using System;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 2001, EventName = "AssemblyLoaderAfterAssemblyLoadError", Level = LogLevel.Error, Message = "An error occured in the even handler AfterAssemblyLoad for assembly {AssemblyName}.")]
	public static partial void AssemblyLoaderAfterAssemblyLoadError(this ILogger logger, string assemblyName, Exception exception);
}
