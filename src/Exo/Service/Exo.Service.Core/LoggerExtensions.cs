using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse the assembly \"{AssemblyName}\" for discovery.")]
	public static partial void DiscoveryAssemblyParsingFailure(this ILogger logger, string assemblyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to prepare the component creation parameters.")]
	public static partial void DiscoveryComponentCreationParametersPreparationFailure(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to prepare the driver creation parameters.")]
	public static partial void DiscoveryDriverCreationParametersPreparationFailure(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Information, Message = "The component {ComponentFriendlyName} was created successfully.")]
	public static partial void DiscoveryComponentCreationSuccess(this ILogger logger, string componentFriendlyName);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred during creation of the component with the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\".")]
	public static partial void DiscoveryComponentCreationFailure(this ILogger logger, string methodName, string typeName, string assemblyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Information, Message = "Aborted the creation of the component for {Key}.")]
	public static partial void DiscoveryComponentCreationAbort(this ILogger logger, string? key);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred during while disposing driver of type \"{TypeName}\" of \"{AssemblyName}\".")]
	public static partial void DiscoveryComponentDisposalFailure(this ILogger logger, string typeName, string assemblyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Information, Message = "The driver for {DeviceFriendlyName} was created successfully for {DeviceName}.")]
	public static partial void DiscoveryDriverCreationSuccess(this ILogger logger, string deviceFriendlyName, string deviceName);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred during creation of the driver with the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\".")]
	public static partial void DiscoveryDriverCreationFailure(this ILogger logger, string methodName, string typeName, string assemblyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" reported the device being offline.")]
	public static partial void DiscoveryDriverCreationDeviceOffline(this ILogger logger, string methodName, string typeName, string assemblyName);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" reported the device {DeviceName} being offline.")]
	public static partial void DiscoveryDriverCreationDeviceOfflineForDevice(this ILogger logger, string methodName, string typeName, string assemblyName, string deviceName);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" reported the device {DeviceName} ({DeviceId}) being offline.")]
	public static partial void DiscoveryDriverCreationDeviceOfflineForDeviceWithId(this ILogger logger, string methodName, string typeName, string assemblyName, string deviceName, DeviceId deviceId);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" reported a missing kernel driver.")]
	public static partial void DiscoveryDriverCreationMissingKernelDriver(this ILogger logger, string methodName, string typeName, string assemblyName);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" reported a missing kernel driver for device {DeviceName}.")]
	public static partial void DiscoveryDriverCreationMissingKernelDriverForDevice(this ILogger logger, string methodName, string typeName, string assemblyName, string deviceName);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" reported a missing kernel driver for device {DeviceName} ({DeviceId}).")]
	public static partial void DiscoveryDriverCreationMissingKernelDriverForDeviceWithId(this ILogger logger, string methodName, string typeName, string assemblyName, string deviceName, DeviceId deviceId);

	[LoggerMessage(Level = LogLevel.Information, Message = "Aborted the creation of the driver for {Key}.")]
	public static partial void DiscoveryDriverCreationAbort(this ILogger logger, string? key);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred during while disposing driver of type \"{TypeName}\" of \"{AssemblyName}\".")]
	public static partial void DiscoveryDriverDisposalFailure(this ILogger logger, string typeName, string assemblyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred during while disposing a component dependency.")]
	public static partial void DiscoveryComponentDependencyDisposalFailure(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to add a reference to the component.")]
	public static partial void DiscoveryComponentAddSharedReferenceFailure(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Debug, Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" successfully registered with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryRegistrationSuccess(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName);

	[LoggerMessage(Level = LogLevel.Warning,Message = "The factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" failed to register with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryRegistrationFailure(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName);

	[LoggerMessage(Level = LogLevel.Warning,Message = "An exception occurred when trying to parse the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryParsingError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Warning,Message = "An exception occurred when trying to register the factory \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" with the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryRegistrationError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Warning,Message = "An exception occurred when trying to read the factory details for \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" for the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryDetailsReadError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Warning,Message = "An exception occurred when trying to persist the factory details for \"{TypeName}.{MethodName}\" of \"{AssemblyName}\" for the service {ComponentFriendlyName}.")]
	public static partial void DiscoveryFactoryDetailsWriteError(this ILogger logger, string methodName, string typeName, string assemblyName, string componentFriendlyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to retrieve the serial number for device \"{DeviceFriendlyName}\".")]
	public static partial void DeviceRegistryDeviceSerialNumberRetrievalFailure(this ILogger logger, string deviceFriendlyName, Exception exception);

	[LoggerMessage(Level = LogLevel.Critical, Message = "An exception occurred when applying changes for restoring the state of the device {DeviceId:B}.")]
	public static partial void LightingServiceRestoreStateApplyChangesError(this ILogger logger, Guid deviceId, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "An exception occurred when processing arrival for lighting device {DeviceId:B} ({DeviceFriendlyName}).")]
	public static partial void LightingServiceDeviceArrivalError(this ILogger logger, Guid deviceId, string deviceFriendlyName, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "An exception occurred when processing removal of lighting device {DeviceId:B} ({DeviceFriendlyName}).")]
	public static partial void LightingServiceDeviceRemovalError(this ILogger logger, Guid deviceId, string deviceFriendlyName, Exception ex);

	[LoggerMessage(Level = LogLevel.Trace, Message = "Acquiring the polling scheduler, with {ReferenceCount} current live references.")]
	public static partial void SensorServicePollingSchedulerAcquire(this ILogger logger, int referenceCount);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Polling scheduler enabled.")]
	public static partial void SensorServicePollingSchedulerEnabled(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Trace, Message = "Releasing the polling scheduler, with {ReferenceCount} current live references.")]
	public static partial void SensorServicePollingSchedulerRelease(this ILogger logger, int referenceCount);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Polling scheduler disabled.")]
	public static partial void SensorServicePollingSchedulerDisabled(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Error, Message = "Sensor watch execution error.")]
	public static partial void SensorServiceSensorStateWatchError(this ILogger logger, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "Grouped query execution error.")]
	public static partial void SensorServiceGroupedQueryError(this ILogger logger, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error while processing arrival of monitor device {DeviceId}.")]
	public static partial void MonitorServiceDeviceArrivalError(this ILogger logger, Guid deviceId, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error while processing removal of monitor device {DeviceId}.")]
	public static partial void MonitorServiceDeviceRemovalError(this ILogger logger, Guid deviceId, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error while publishing setting values for device {DeviceId}.")]
	public static partial void MonitorServiceSettingPublishError(this ILogger logger, Guid deviceId, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error while refreshing setting values for device {DeviceId}.")]
	public static partial void MonitorServiceSettingRefreshError(this ILogger logger, Guid deviceId, Exception ex);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error while retrieving the value for monitor setting {MonitorSetting} of device {DeviceId}.")]
	public static partial void MonitorServiceSettingValueRetrievalError(this ILogger logger, Guid deviceId, MonitorSetting monitorSetting, Exception ex);
}
