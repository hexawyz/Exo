using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Debug, Message = "HID device discovery is starting…")]
	public static partial void HidDiscoveryStarting(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "HID device discovery has started.")]
	public static partial void HidDiscoveryStarted(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Arrival of HID device: \"{DeviceName}\".")]
	public static partial void HidDeviceArrival(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Pending removal of HID device: \"{DeviceName}\".")]
	public static partial void HidDevicePendingRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Removal of HID device: \"{DeviceName}\".")]
	public static partial void HidDeviceRemoval(this ILogger logger, string deviceName);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory did not define any valid keys for HID discovery.")]
	public static partial void HidFactoryMissingKeys(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4}.")]
	public static partial void HidVendorDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4}.")]
	public static partial void HidProductDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines two keys for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} Version {VersionNumber:X4}.")]
	public static partial void HidVersionedProductDuplicateKey
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		ushort versionNumber
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} was already registered.")]
	public static partial void HidVendorRegisteredTwice
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} was already registered.")]
	public static partial void HidProductRegistrationConflict
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} Version {VersionNumber:X4} was already registered.")]
	public static partial void HidVersionedProductRegistrationConflict
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		ushort versionNumber
	);
}
