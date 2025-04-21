using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Debug, Message = "Monitor device discovery is starting…")]
	public static partial void MonitorDiscoveryStarting(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Monitor device discovery has started.")]
	public static partial void MonitorDiscoveryStarted(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Arrival of Monitor device: \"{DeviceName}\".")]
	public static partial void MonitorDeviceArrival(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Pending removal of Monitor device: \"{DeviceName}\".")]
	public static partial void MonitorDevicePendingRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Removal of Monitor device: \"{DeviceName}\".")]
	public static partial void MonitorDeviceRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse the monitor name \"{RawMonitorName}\".")]
	public static partial void MonitorNameParsingFailure(this ILogger logger, string? rawMonitorName);

	[LoggerMessage(Level = LogLevel.Warning, Message = "The vendor ID \"{VendorId:X4}\" could not be parsed into a valid vendor name.")]
	public static partial void MonitorInvalidVendorId(this ILogger logger, ushort vendorId);

	[LoggerMessage(EventName = "MonitorVendorIdParsingFailure", Level = LogLevel.Warning, Message = "Failed to parse the vendor ID \"{RawVendorId}\".")]
	public static partial void MonitorVendorIdParsingFailure(this ILogger logger, string? rawVendorId);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory did not define any valid keys for Monitor discovery")]
	public static partial void MonitorFactoryMissingKeys(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for Vendor {VendorName}.")]
	public static partial void MonitorVendorDuplicateKey
	(
		this ILogger logger,
		string vendorName
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for Monitor {VendorName}{ProductId:X4}.")]
	public static partial void MonitorProductDuplicateKey
	(
		this ILogger logger,
		string vendorName,
		ushort productId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for Vendor {VendorName} was already registered.")]
	public static partial void MonitorVendorRegisteredTwice
	(
		this ILogger logger,
		string vendorName
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for Monitor {VendorName}{ProductId:X4} was already registered.")]
	public static partial void MonitorProductRegistrationConflict
	(
		this ILogger logger,
		string vendorName,
		ushort productId
	);
}
