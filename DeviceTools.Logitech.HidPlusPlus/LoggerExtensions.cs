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
}
