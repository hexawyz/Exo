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


	[LoggerMessage(EventId = 10_304, EventName = "UiSensorServiceSensorWatchStart", Level = LogLevel.Debug, Message = "Started watching sensor values for sensor {SensorId} of device {DeviceId} as stream #{StreamId}.")]
	public static partial void UiSensorServiceSensorWatchStart(this ILogger logger, Guid deviceId, Guid sensorId, uint streamId);

	[LoggerMessage(EventId = 10_305, EventName = "UiSensorServiceSensorWatchStop", Level = LogLevel.Debug, Message = "Stopped watching sensor values for stream #{StreamId}.")]
	public static partial void UiSensorServiceSensorWatchStop(this ILogger logger, uint streamId);

	[LoggerMessage(EventId = 10_306, EventName = "UiSensorServiceSensorWatchNotification", Level = LogLevel.Trace, Message = "New data point for stream #{StreamId}: {SensorDataPointValue} [{SensorDataPointDateTime}].")]
	public static partial void UiSensorServiceSensorWatchNotification(this ILogger logger, uint streamId, DateTime sensorDataPointDateTime, object sensorDataPointValue);

	[LoggerMessage(EventId = 10_307, EventName = "UiSensorServiceSensorWatchError", Level = LogLevel.Error, Message = "An error occurred while processing stream #{StreamId}.")]
	public static partial void UiSensorServiceSensorWatchError(this ILogger logger, uint streamId, Exception ex);
}
