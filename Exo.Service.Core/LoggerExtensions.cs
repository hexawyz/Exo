using System;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, EventName = "HidDeviceArrival", Level = LogLevel.Debug, Message = "Arrival of HID device: \"{DeviceName}\".")]
	public static partial void HidDeviceArrival(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1002, EventName = "HidDeviceRemoval", Level = LogLevel.Debug, Message = "Removal of HID device: \"{DeviceName}\".")]
	public static partial void HidDeviceRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1003, EventName = "HidDriverUnregisterSuccess", Level = LogLevel.Information, Message = "Succesfully unregistered the driver {DriverType} for device \"{DeviceName}\".")]
	public static partial void HidDriverUnregisterSuccess(this ILogger logger, Type driverType, string deviceName);

	[LoggerMessage(EventId = 1004, EventName = "HidDriverUnregisterFailure", Level = LogLevel.Warning, Message = "Failed to unregister the driver {DriverType} for device \"{DeviceName}\".")]
	public static partial void HidDriverUnregisterFailure(this ILogger logger, Type driverType, string deviceName);
}
