using System;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 2001, EventName = "GrpcLightingServiceEffectInformationRetrievalError", Level = LogLevel.Error, Message = "An error occurred when retrieving informations on effect {EffectType}.")]
	public static partial void GrpcLightingServiceEffectInformationRetrievalError(this ILogger logger, Type effectType, Exception exception);

	[LoggerMessage(EventId = 2002, EventName = "GrpcLightingServiceEffectApplicationError", Level = LogLevel.Error, Message = "An error occurred when applying the effect {EffectId} on Zone {ZoneId} of device {DeviceId}.")]
	public static partial void GrpcLightingServiceEffectApplicationError(this ILogger logger, Guid deviceId, Guid zoneId, Guid effectId, Exception exception);
}
