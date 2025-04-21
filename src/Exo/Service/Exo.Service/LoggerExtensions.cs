using System;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Debug, Message = "Created a load context for assembly \"{AssemblyName}\".")]
	public static partial void AssemblyLoadContextCreated(this ILogger logger, string assemblyName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "The context for assembly \"{AssemblyName}\" is unloading.")]
	public static partial void AssemblyLoadContextUnloading(this ILogger logger, string assemblyName);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred in the even handler AfterAssemblyLoad for assembly {AssemblyName}.")]
	public static partial void AssemblyLoaderAfterAssemblyLoadError(this ILogger logger, string assemblyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Started watching sensor values for sensor {SensorId} of device {DeviceId} as stream #{StreamId}.")]
	public static partial void UiSensorServiceSensorWatchStart(this ILogger logger, Guid deviceId, Guid sensorId, uint streamId);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Stopped watching sensor values for stream #{StreamId}.")]
	public static partial void UiSensorServiceSensorWatchStop(this ILogger logger, uint streamId);

	[LoggerMessage(Level = LogLevel.Trace, Message = "New data point for stream #{StreamId}: {SensorDataPointValue} [{SensorDataPointDateTime}].")]
	public static partial void UiSensorServiceSensorWatchNotification(this ILogger logger, uint streamId, DateTime sensorDataPointDateTime, object sensorDataPointValue);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred while processing stream #{StreamId}.")]
	public static partial void UiSensorServiceSensorWatchError(this ILogger logger, uint streamId, Exception ex);
}
