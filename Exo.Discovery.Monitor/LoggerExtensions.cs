using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, EventName = "MonitorDiscoveryStarting", Level = LogLevel.Debug, Message = "Monitor device discovery is starting…")]
	public static partial void MonitorDiscoveryStarting(this ILogger logger);

	[LoggerMessage(EventId = 1002, EventName = "MonitorDiscoveryStarted", Level = LogLevel.Debug, Message = "Monitor device discovery has started.")]
	public static partial void MonitorDiscoveryStarted(this ILogger logger);

	[LoggerMessage(EventId = 1003, EventName = "DisplayAdapterDeviceArrival", Level = LogLevel.Debug, Message = "Arrival of Monitor device: \"{DeviceName}\".")]
	public static partial void MonitorDeviceArrival(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1004, EventName = "DisplayAdapterDevicePendingRemoval", Level = LogLevel.Debug, Message = "Pending removal of Monitor device: \"{DeviceName}\".")]
	public static partial void MonitorDevicePendingRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1005, EventName = "DisplayAdapterDeviceRemoval", Level = LogLevel.Debug, Message = "Removal of Monitor device: \"{DeviceName}\".")]
	public static partial void MonitorDeviceRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1006, EventName = "MonitorNameParsingFailure", Level = LogLevel.Warning, Message = "Failed to parse the monitor name \"{RawMonitorName}\" for factory \"{FactoryId}\".")]
	public static partial void MonitorNameParsingFailure(this ILogger logger, Guid factoryId, string? rawMonitorName);

	[LoggerMessage(EventId = 1007,
		EventName = "MonitorInvalidVendorId",
		Level = LogLevel.Warning,
		Message = "The vendor ID \"{VendorId:X4}\" for factory \"{FactoryId}\" could not be parsed into a valid vendor name.")]
	public static partial void MonitorInvalidVendorId(this ILogger logger, Guid factoryId, ushort vendorId);

	[LoggerMessage(EventId = 1008, EventName = "MonitorVendorIdParsingFailure", Level = LogLevel.Warning, Message = "Failed to parse the vendor ID \"{RawVendorId}\" for factory \"{FactoryId}\".")]
	public static partial void MonitorVendorIdParsingFailure(this ILogger logger, Guid factoryId, string? rawVendorId);

	[LoggerMessage(EventId = 1009,
		EventName = "MonitorFactoryMissingKeys",
		Level = LogLevel.Error,
		Message = "The factory \"{FactoryId}\" did not define any valid keys for Monitor discovery")]
	public static partial void MonitorFactoryMissingKeys(this ILogger logger, Guid factoryId);

	[LoggerMessage(EventId = 1010,
		EventName = "MonitorVendorDuplicateKey",
		Level = LogLevel.Error,
		Message = "The factory defines two keys for Vendor {VendorName}.")]
	public static partial void MonitorVendorDuplicateKey
	(
		this ILogger logger,
		string vendorName
	);

	[LoggerMessage(EventId = 1011,
		EventName = "MonitorProductDuplicateKey",
		Level = LogLevel.Error,
		Message = "The factory defines two keys for Monitor {VendorName}{ProductId:X4}.")]
	public static partial void MonitorProductDuplicateKey
	(
		this ILogger logger,
		string vendorName,
		ushort productId
	);

	[LoggerMessage(EventId = 1012,
		EventName = "MonitorVendorRegistrationConflict",
		Level = LogLevel.Error,
		Message = "A factory for Vendor {VendorName} was already registered.")]
	public static partial void MonitorVendorRegisteredTwice
	(
		this ILogger logger,
		string vendorName
	);

	[LoggerMessage(EventId = 1013,
		EventName = "MonitorProductRegistrationConflict",
		Level = LogLevel.Error,
		Message = "A factory for Monitor {VendorName}{ProductId:X4} was already registered.")]
	public static partial void MonitorProductRegistrationConflict
	(
		this ILogger logger,
		string vendorName,
		ushort productId
	);
}
