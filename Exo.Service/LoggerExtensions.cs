using System;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, EventName = "AssemblyLoadContextCreated", Level = LogLevel.Debug, Message = "Created a load context for assembly \"{AssemblyName}\".")]
	public static partial void AssemblyLoadContextCreated(this ILogger logger, string assemblyName);

	[LoggerMessage(EventId = 1002, EventName = "AssemblyLoadContextUnloading", Level = LogLevel.Debug, Message = "The context for assembly \"{AssemblyName}\" is unloading.")]
	public static partial void AssemblyLoadContextUnloading(this ILogger logger, string assemblyName);

	[LoggerMessage(EventId = 1003, EventName = "AssemblyLoaderAfterAssemblyLoadError", Level = LogLevel.Error, Message = "An error occurred in the even handler AfterAssemblyLoad for assembly {AssemblyName}.")]
	public static partial void AssemblyLoaderAfterAssemblyLoadError(this ILogger logger, string assemblyName, Exception exception);
}
