using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, EventName = "PciDiscoveryStarting", Level = LogLevel.Debug, Message = "PCI device discovery is starting…")]
	public static partial void PciDiscoveryStarting(this ILogger logger);

	[LoggerMessage(EventId = 1002, EventName = "PciDiscoveryStarted", Level = LogLevel.Debug, Message = "PCI device discovery has started.")]
	public static partial void PciDiscoveryStarted(this ILogger logger);

	[LoggerMessage(EventId = 1003, EventName = "DisplayAdapterDeviceArrival", Level = LogLevel.Debug, Message = "Arrival of Display Adapter device: \"{DeviceName}\".")]
	public static partial void DisplayAdapterDeviceArrival(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1004, EventName = "DisplayAdapterDevicePendingRemoval", Level = LogLevel.Debug, Message = "Pending removal of Display Adapter device: \"{DeviceName}\".")]
	public static partial void DisplayAdapterDevicePendingRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1005, EventName = "DisplayAdapterDeviceRemoval", Level = LogLevel.Debug, Message = "Removal of Display Adapter device: \"{DeviceName}\".")]
	public static partial void DisplayAdapterDeviceRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1006,
		EventName = "PciFactoryMissingKeys",
		Level = LogLevel.Error,
		Message = "The factory \"{FactoryId}\" did not define any valid keys for PCI discovery.")]
	public static partial void PciFactoryMissingKeys(this ILogger logger, Guid factoryId);

	[LoggerMessage(EventId = 1007,
		EventName = "PciVendorDuplicateKey",
		Level = LogLevel.Error,
		Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4}.")]
	public static partial void PciVendorDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId
	);

	[LoggerMessage(EventId = 1008,
		EventName = "PciProductDuplicateKey",
		Level = LogLevel.Error,
		Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4}.")]
	public static partial void PciProductDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId
	);

	[LoggerMessage(EventId = 1009,
		EventName = "PciVersionedProductDuplicateKey",
		Level = LogLevel.Error,
		Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} Version {VersionNumber:X4}.")]
	public static partial void PciVersionedProductDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		ushort versionNumber
	);

	[LoggerMessage(EventId = 1010,
		EventName = "PciVendorRegistrationConflict",
		Level = LogLevel.Error,
		Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} was already registered.")]
	public static partial void PciVendorRegisteredTwice
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId
	);

	[LoggerMessage(EventId = 1011,
		EventName = "PciProductRegistrationConflict",
		Level = LogLevel.Error,
		Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} was already registered.")]
	public static partial void PciProductRegistrationConflict
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId
	);

	[LoggerMessage(EventId = 1012,
		EventName = "PciVersionedProductRegistrationConflict",
		Level = LogLevel.Error,
		Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} Version {VersionNumber:X4} was already registered.")]
	public static partial void PciVersionedProductRegistrationConflict
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		ushort versionNumber
	);
}
