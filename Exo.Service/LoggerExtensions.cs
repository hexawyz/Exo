using System;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, EventName = "AssemblyLoaderAfterAssemblyLoadError", Level = LogLevel.Error, Message = "An error occured in the even handler AfterAssemblyLoad for assembly {AssemblyName}.")]
	public static partial void AssemblyLoaderAfterAssemblyLoadError(this ILogger logger, string assemblyName, Exception exception);

	[LoggerMessage(EventId = 2001, EventName = "GrpcLightingServiceEffectInformationRetrievalError", Level = LogLevel.Error, Message = "An error occurred when retrieving informations on effect {EffectType}.")]
	public static partial void GrpcLightingServiceEffectInformationRetrievalError(this ILogger logger, Type effectType, Exception exception);
}
