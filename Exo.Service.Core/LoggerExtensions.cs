using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, EventName = "DiscoveryAssemblyParsingFailure", Level = LogLevel.Error, Message = "Failed to parse the assembly \"{AssemblyName}\" for discovery.")]
	public static partial void DiscoveryAssemblyParsingFailure(this ILogger logger, string assemblyName, Exception exception);

	[LoggerMessage(EventId = 1002, EventName = "DiscoveryComponentCreationParametersPreparationFailure", Level = LogLevel.Error, Message = "Failed to prepare the component creation parameters.")]
	public static partial void DiscoveryComponentCreationParametersPreparationFailure(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 1003, EventName = "DiscoveryDriverCreationParametersPreparationFailure", Level = LogLevel.Error, Message = "Failed to prepare the driver creation parameters.")]
	public static partial void DiscoveryDriverCreationParametersPreparationFailure(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 1004, EventName = "DiscoveryComponentCreationSuccess", Level = LogLevel.Information, Message = "The component {ComponentFriendlyName} was created successfully.")]
	public static partial void DiscoveryComponentCreationSuccess(this ILogger logger, string componentFriendlyName);

	[LoggerMessage(EventId = 1005, EventName = "DiscoveryComponentCreationFailure", Level = LogLevel.Error, Message = "An error occurred during creation of the component with the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\".")]
	public static partial void DiscoveryComponentCreationFailure(this ILogger logger, string methodName, string typeName, string assemblyName, Exception exception);

	[LoggerMessage(EventId = 1006, EventName = "DiscoveryDriverCreationSuccess", Level = LogLevel.Information, Message = "The driver for {DeviceFriendlyName} was created successfully for {DeviceName}.")]
	public static partial void DiscoveryDriverCreationSuccess(this ILogger logger, string deviceFriendlyName, string deviceName);

	[LoggerMessage(EventId = 1007, EventName = "DiscoveryDriverCreationFailure", Level = LogLevel.Error, Message = "An error occurred during creation of the driver with the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\".")]
	public static partial void DiscoveryDriverCreationFailure(this ILogger logger, string methodName, string typeName, string assemblyName, Exception exception);

	[LoggerMessage(EventId = 1008,
		EventName = "DiscoveryFactoryRegistrationSuccess",
		Level = LogLevel.Debug,
		Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" successfully registered with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryRegistrationSuccess(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName);

	[LoggerMessage(EventId = 1009,
		EventName = "DiscoveryFactoryRegistrationFailure",
		Level = LogLevel.Warning,
		Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" failed to register with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryRegistrationFailure(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName);

	[LoggerMessage(EventId = 1010,
		EventName = "DiscoveryFactoryParsingError",
		Level = LogLevel.Warning,
		Message = "An exception occurred when trying to parse the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryParsingError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(EventId = 1011,
		EventName = "DiscoveryFactoryRegistrationError",
		Level = LogLevel.Warning,
		Message = "An exception occurred when trying to register the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryRegistrationError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(EventId = 1012,
		EventName = "DiscoveryFactoryDetailsReadError",
		Level = LogLevel.Warning,
		Message = "An exception occurred when trying to read the factory details for \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" for the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryDetailsReadError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(EventId = 1013,
		EventName = "DiscoveryFactoryDetailsWriteError",
		Level = LogLevel.Warning,
		Message = "An exception occurred when trying to persist the factory details for \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" for the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryDetailsWriteError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(EventId = 2001, EventName = "DeviceRegistryDeviceSerialNumberRetrievalFailure", Level = LogLevel.Error, Message = "Failed to retrieve the serial number for device \"{DeviceFriendlyName}\".")]
	public static partial void DeviceRegistryDeviceSerialNumberRetrievalFailure(this ILogger logger, string deviceFriendlyName, Exception exception);

	[LoggerMessage(EventId = 3001, Level = LogLevel.Critical, Message = "An exception occurred when applying changes for restoring the state of the device {DeviceId:B}.")]
	public static partial void LightingServiceRestoreStateApplyChangesError(this ILogger logger, Guid deviceId, Exception ex);

	[LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "An exception occurred when processing arrival for lighting device {DeviceId:B} ({DeviceFriendlyName}).")]
	public static partial void LightingServiceDeviceArrivalError(this ILogger logger, Guid deviceId, string deviceFriendlyName, Exception ex);

	[LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "An exception occurred when processing removal of lighting device {DeviceId:B} ({DeviceFriendlyName}).")]
	public static partial void LightingServiceDeviceRemovalError(this ILogger logger, Guid deviceId, string deviceFriendlyName, Exception ex);

	[LoggerMessage(EventId = 4101, Level = LogLevel.Trace, Message = "Acquiring the polling scheduler, with {ReferenceCount} current live references.")]
	public static partial void SensorServicePollingSchedulerAcquire(this ILogger logger, int referenceCount);

	[LoggerMessage(EventId = 4102, Level = LogLevel.Debug, Message = "Polling scheduler enabled.")]
	public static partial void SensorServicePollingSchedulerEnabled(this ILogger logger);

	[LoggerMessage(EventId = 4103, Level = LogLevel.Trace, Message = "Releasing the polling scheduler, with {ReferenceCount} current live references.")]
	public static partial void SensorServicePollingSchedulerRelease(this ILogger logger, int referenceCount);

	[LoggerMessage(EventId = 4104, Level = LogLevel.Debug, Message = "Polling scheduler disabled.")]
	public static partial void SensorServicePollingSchedulerDisabled(this ILogger logger);

	[LoggerMessage(EventId = 4301, Level = LogLevel.Debug, Message = "Sensor watch execution error.")]
	public static partial void SensorServiceSensorStateWatchError(this ILogger logger, Exception ex);

	[LoggerMessage(EventId = 4301, Level = LogLevel.Debug, Message = "Grouped query execution error.")]
	public static partial void SensorServiceGroupedQueryError(this ILogger logger, Exception ex);
}
