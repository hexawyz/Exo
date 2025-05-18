using Microsoft.Extensions.Logging;

namespace Exo.Settings.Ui;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Error, Message = "XAML Binding failed: {Message}")]
	public static partial void XamlBindingFailed(this ILogger logger, string message);

	[LoggerMessage(Level = LogLevel.Error, Message = "XAML Resource Reference failed: {Message}")]
	public static partial void XamlResourceReferenceFailed(this ILogger logger, string message);

	[LoggerMessage(Level = LogLevel.Error, Message = "An unhandled exception occurred: {Message}")]
	public static partial void XamlUnhandledException(this ILogger logger, string message, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An unhandled exception occurred.")]
	public static partial void UnhandledException(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when processing service messages.")]
	public static partial void ServiceConnectionException(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when processing a device notification.")]
	public static partial void DeviceNotificationError(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to open the image.")]
	public static partial void ImageOpenError(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = @"The image could not be added because the name ""{ImageName}"" is already in use.")]
	public static partial void ImageDuplicateName(this ILogger logger, string imageName);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to add an image to the library.")]
	public static partial void ImageAddError(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to remove the image {ImageName} (ID {ImageId}) from the library.")]
	public static partial void ImageRemoveError(this ILogger logger, string imageName, UInt128 imageId, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when applying lighting changes to {DeviceName}.")]
	public static partial void LightingApplyError(this ILogger logger, string deviceName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when switching the light {LightName} of {DeviceName}.")]
	public static partial void LightSwitchError(this ILogger logger, string lightName, string deviceName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when setting the brightness of the light {LightName} of {DeviceName}.")]
	public static partial void LightBrightnessError(this ILogger logger, string lightName, string deviceName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when setting the temperature of the light {LightName} of {DeviceName}.")]
	public static partial void LightTemperatureError(this ILogger logger, string lightName, string deviceName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when setting the low-power mode battery threshold of {DeviceName}.")]
	public static partial void PowerLowPowerModeBatteryThresholdError(this ILogger logger, string deviceName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when setting the idle sleep delay of {DeviceName}.")]
	public static partial void PowerIdleSleepTimerError(this ILogger logger, string deviceName, Exception exception);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred when setting the wireless brightness of {DeviceName}.")]
	public static partial void PowerWirelessBrightnessError(this ILogger logger, string deviceName, Exception exception);
}
