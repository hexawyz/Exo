using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using Microsoft.Extensions.Logging;
using static DeviceTools.Logitech.HidPlusPlus.HidPlusPlusDevice;

namespace DeviceTools.Logitech.HidPlusPlus;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 65000, Level = LogLevel.Error, Message = "An exception occurred when handling a HID++ notification message.")]
	public static partial void HidPlusPlusTransportNotificationHandlerException(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 65001, Level = LogLevel.Error, Message = "An exception occurred.")]
	public static partial void RegisterAccessReceiverDeviceUnhandledException(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 65002, Level = LogLevel.Error, Message = "An exception occurred in the {deviceEventKind} handler.")]
	public static partial void RegisterAccessReceiverDeviceEventHandlerException(this ILogger logger, DeviceEventKind deviceEventKind, Exception exception);

	[LoggerMessage(EventId = 65003, Level = LogLevel.Error, Message = "An exception occurred while handling the HID++ 2.0 event {eventId} of feature {feature}.")]
	public static partial void FeatureAccessFeatureHandlerException(this ILogger logger, HidPlusPlusFeature feature, byte eventId, Exception exception);

	[LoggerMessage(EventId = 65004, Level = LogLevel.Information, Message = "Connected HID++ 1.0 {ProductId:X4} device {DeviceSerialNumber}, named {DeviceFriendlyName}.")]
	public static partial void RegisterAccessDeviceConnected(this ILogger logger, ushort productId, string? deviceFriendlyName, string? deviceSerialNumber);

	[LoggerMessage(EventId = 65005, Level = LogLevel.Information, Message = "Connected HID++ 2.0 {ProductId:X4} device {DeviceSerialNumber}, named {DeviceFriendlyName}.")]
	public static partial void FeatureAccessDeviceConnected(this ILogger logger, ushort productId, string? deviceFriendlyName, string? deviceSerialNumber);

	[LoggerMessage(EventId = 65006, Level = LogLevel.Debug, Message = "Feature {FeatureIndex} of {DeviceSerialNumber} is {Feature:X} ({Feature:G}) of type {FeatureType} with version {FeatureVersion}.")]
	public static partial void FeatureAccessDeviceKnownFeature(this ILogger logger, string? deviceSerialNumber, byte featureIndex, HidPlusPlusFeature feature, HidPlusPlusFeatureTypes featureType, byte featureVersion);

	[LoggerMessage(EventId = 65007, Level = LogLevel.Debug, Message = "Feature {FeatureIndex} of {DeviceSerialNumber} is {Feature:X} of type {FeatureType} with version {FeatureVersion}.")]
	public static partial void FeatureAccessDeviceUnknownFeature(this ILogger logger, string? deviceSerialNumber, byte featureIndex, HidPlusPlusFeature feature, HidPlusPlusFeatureTypes featureType, byte featureVersion);

	[LoggerMessage(
		EventId = 65008,
		Level = LogLevel.Debug,
		Message = "Device {DeviceSerialNumber} has control {ControlId} (Task: {TaskId}, Position: {Position}, GroupNumber: {GroupNumber}, GroupMask: {GroupMask}, Flags: {Flags}, Capabilities: {ReportingCapabilities}).")]
	public static partial void FeatureAccessDevice1B04ControlIdWithPositionAndGroup
	(
		this ILogger logger,
		string? deviceSerialNumber,
		ControlId controlId,
		TaskId taskId,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlFlags flags,
		byte position,
		byte groupNumber,
		byte groupMask,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlReportingCapabilities reportingCapabilities
	);

	[LoggerMessage(
		EventId = 65009,
		Level = LogLevel.Debug,
		Message = "Device {DeviceSerialNumber} has control {ControlId} (Task: {TaskId}, GroupNumber: {GroupNumber}, GroupMask: {GroupMask}, Flags: {Flags}, Capabilities: {ReportingCapabilities}).")]
	public static partial void FeatureAccessDevice1B04ControlIdWithGroup
	(
		this ILogger logger,
		string? deviceSerialNumber,
		ControlId controlId,
		TaskId taskId,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlFlags flags,
		byte groupNumber,
		byte groupMask,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlReportingCapabilities reportingCapabilities
	);

	[LoggerMessage(
		EventId = 65010,
		Level = LogLevel.Debug,
		Message = "Device {DeviceSerialNumber} has non-remappable control {ControlId} (Task: {TaskId}, Position: {Position}, Flags: {Flags}, Capabilities: {ReportingCapabilities}).")]
	public static partial void FeatureAccessDevice1B04NonRemappableControlIdWithPosition
	(
		this ILogger logger,
		string? deviceSerialNumber,
		ControlId controlId,
		TaskId taskId,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlFlags flags,
		byte position,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlReportingCapabilities reportingCapabilities
	);

	[LoggerMessage(
		EventId = 65011,
		Level = LogLevel.Debug,
		Message = "Device {DeviceSerialNumber} has non-remappable control {ControlId} (Task: {TaskId}, Flags: {Flags}, Capabilities: {ReportingCapabilities}).")]
	public static partial void FeatureAccessDevice1B04NonRemappableControlId
	(
		this ILogger logger,
		string? deviceSerialNumber,
		ControlId controlId,
		TaskId taskId,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlFlags flags,
		KeyboardReprogrammableKeysAndMouseButtonsV5.ControlReportingCapabilities reportingCapabilities
	);
}
