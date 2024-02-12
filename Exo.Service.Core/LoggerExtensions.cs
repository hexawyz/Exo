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

	[LoggerMessage(EventId = 1004, EventName = "DiscoveryComponentCreationSuccess", Level = LogLevel.Information, Message = "The component {ComponentFriendlyName} was created successfully.")]
	public static partial void DiscoveryComponentCreationSuccess(this ILogger logger, string componentFriendlyName);

	[LoggerMessage(EventId = 1005, EventName = "DiscoveryComponentCreationFailure", Level = LogLevel.Error, Message = "An error occurred during creation of the component with the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\".")]
	public static partial void DiscoveryComponentCreationFailure(this ILogger logger, string methodName, string typeName, string assemblyName, Exception exception);

	[LoggerMessage(EventId = 1006, EventName = "DiscoveryDriverCreationSuccess", Level = LogLevel.Information, Message = "The driver for {DeviceFriendlyName} was created successfully for {DeviceName}.")]
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
		EventName = "DiscoveryFactoryRegistrationError",
		Level = LogLevel.Warning,
		Message = "An exception occurred when trying to register the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryRegistrationError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(EventId = 3000, EventName = "DevicePropertiesRetrievalError", Level = LogLevel.Error, Message = "Failed to retrieve properties of device: \"{DeviceName}\".")]
	public static partial void DevicePropertiesRetrievalError(this ILogger logger, string deviceName, Exception exception);

	[LoggerMessage(EventId = 2000, Level = LogLevel.Critical, Message = "An exception occurred when applying changes for restoring the state of the device {deviceId:B}.")]
	public static partial void LightingServiceRestoreStateApplyChangesError(this ILogger logger, Guid deviceId, Exception ex);
}
