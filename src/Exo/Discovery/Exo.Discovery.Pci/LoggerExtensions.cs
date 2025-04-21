using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Debug, Message = "PCI device discovery is starting…")]
	public static partial void PciDiscoveryStarting(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "PCI device discovery has started.")]
	public static partial void PciDiscoveryStarted(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Arrival of Display Adapter device: \"{DeviceName}\".")]
	public static partial void DisplayAdapterDeviceArrival(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Pending removal of Display Adapter device: \"{DeviceName}\".")]
	public static partial void DisplayAdapterDevicePendingRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Removal of Display Adapter device: \"{DeviceName}\".")]
	public static partial void DisplayAdapterDeviceRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory did not define any valid keys for PCI discovery.")]
	public static partial void PciFactoryMissingKeys(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4}.")]
	public static partial void PciVendorDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4}.")]
	public static partial void PciProductDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} Version {VersionNumber:X4}.")]
	public static partial void PciVersionedProductDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		ushort versionNumber
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} was already registered.")]
	public static partial void PciVendorRegisteredTwice
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} was already registered.")]
	public static partial void PciProductRegistrationConflict
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} Version {VersionNumber:X4} was already registered.")]
	public static partial void PciVersionedProductRegistrationConflict
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		ushort versionNumber
	);
}
