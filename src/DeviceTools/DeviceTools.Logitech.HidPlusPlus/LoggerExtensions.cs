using Microsoft.Extensions.Logging;
using static DeviceTools.Logitech.HidPlusPlus.HidPlusPlusDevice;

namespace DeviceTools.Logitech.HidPlusPlus;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "An exception occurred when handling a HID++ notification message.")]
	public static partial void HidPlusPlusTransportNotificationHandlerException(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "An exception occurred.")]
	public static partial void RegisterAccessReceiverDeviceUnhandledException(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "An exception occurred in the {deviceEventKind} handler.")]
	public static partial void RegisterAccessReceiverDeviceEventHandlerException(this ILogger logger, DeviceEventKind deviceEventKind, Exception exception);

	[LoggerMessage(EventId = 2101, Level = LogLevel.Error, Message = "An exception occurred while handling the HID++ 2.0 event {eventId} of feature {feature}.")]
	public static partial void FeatureAccessFeatureHandlerException(this ILogger logger, HidPlusPlusFeature feature, byte eventId, Exception exception);

	[LoggerMessage(EventId = 10101, Level = LogLevel.Information, Message = "Connected HID++ 1.0 {ProductId:X4} device {DeviceSerialNumber}, named {DeviceFriendlyName}.")]
	public static partial void RegisterAccessDeviceConnected(this ILogger logger, ushort productId, string? deviceFriendlyName, string deviceSerialNumber);

	[LoggerMessage(EventId = 10102, Level = LogLevel.Information, Message = "Connected HID++ 2.0 {ProductId:X4} device {DeviceSerialNumber}, named {DeviceFriendlyName}.")]
	public static partial void FeatureAccessDeviceConnected(this ILogger logger, ushort productId, string? deviceFriendlyName, string deviceSerialNumber);

	[LoggerMessage(EventId = 10103, Level = LogLevel.Debug, Message = "Feature {FeatureIndex} of {DeviceSerialNumber} is {Feature:X} ({Feature:G}) of type {FeatureType} with version {FeatureVersion}.")]
	public static partial void FeatureAccessDeviceKnownFeature(this ILogger logger, string deviceSerialNumber, byte featureIndex, HidPlusPlusFeature feature, HidPlusPlusFeatureTypes featureType, byte featureVersion);

	[LoggerMessage(EventId = 10104, Level = LogLevel.Debug, Message = "Feature {FeatureIndex} of {DeviceSerialNumber} is {Feature:X} of type {FeatureType} with version {FeatureVersion}.")]
	public static partial void FeatureAccessDeviceUnknownFeature(this ILogger logger, string deviceSerialNumber, byte featureIndex, HidPlusPlusFeature feature, HidPlusPlusFeatureTypes featureType, byte featureVersion);

}
