using System;
using System.Collections.Generic;
using DeviceTools;
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

	[LoggerMessage(EventId = 1005, EventName = "HidAssemblyParsingFailure", Level = LogLevel.Error, Message = "Failed to parse the assembly \"{AssemblyName}\" for HID drivers.")]
	public static partial void HidAssemblyParsingFailure(this ILogger logger, string assemblyName, Exception exception);

	[LoggerMessage(EventId = 1006,
		EventName = "HidVendorRegisteredTwice",
		Level = LogLevel.Error,
		Message = "Failed to register {VendorIdSource} Vendor ID {VendorId:X4} a second time for \"{Type2Name}\" in \"{Assembly2Name}\". It was previously registered by \"{Type1Name}\" in \"{Assembly1Name}\".")]
	public static partial void HidVendorRegisteredTwice
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		string type2Name,
		string assembly2Name,
		string type1Name,
		string assembly1Name,
		Exception exception
	);

	[LoggerMessage(EventId = 1007,
		EventName = "HidProductRegisteredTwice",
		Level = LogLevel.Error,
		Message = "Failed to register {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} a second time for \"{Type2Name}\" in \"{Assembly2Name}\". It was previously registered by \"{Type1Name}\" in \"{Assembly1Name}\".")]
	public static partial void HidProductRegisteredTwice
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		string type2Name,
		string assembly2Name,
		string type1Name,
		string assembly1Name,
		Exception exception
	);

	[LoggerMessage(EventId = 1008,
		EventName = "HidVersionedProductRegisteredTwice",
		Level = LogLevel.Error,
		Message = "Failed to register {VendorIdSource} Vendor ID {VendorId:X4} Product ID {ProductId:X4} Version {VersionNumber:X4} a second time for \"{Type2Name}\" in \"{Assembly2Name}\". It was previously registered by \"{Type1Name}\" in \"{Assembly1Name}\".")]
	public static partial void HidVersionedProductRegisteredTwice
	(
		this ILogger logger,
		VendorIdSource vendorIdSource,
		ushort vendorId,
		ushort productId,
		ushort versionNumber,
		string type2Name,
		string assembly2Name,
		string type1Name,
		string assembly1Name,
		Exception exception
	);

	[LoggerMessage(EventId = 1009,
		EventName = "HidDeviceDriverMatch",
		Level = LogLevel.Information,
		Message = "The driver \"{TypeName}\" in \"{AssemblyName}\" is a match for the device \"{DeviceName}\".")]
	public static partial void HidDeviceDriverMatch(this ILogger logger, string typeName, string assemblyName, string deviceName);

	[LoggerMessage(EventId = 1010, EventName = "HidDeviceDriverAlreadyAssigned", Level = LogLevel.Debug, Message = "The device \"{DeviceName}\" has already been assigned a driver.")]
	public static partial void HidDeviceDriverAlreadyAssigned(this ILogger logger, string deviceName);

	[LoggerMessage(EventId = 1011, EventName = "HidDriverCreationFailure", Level = LogLevel.Error, Message = "Failed to create an instance of \"{TypeName}\" from \"{AssemblyName}\" for the device \"{DeviceName}\".")]
	public static partial void HidDriverCreationFailure(this ILogger logger, string typeName, string assemblyName, string deviceName, Exception exception);

	[LoggerMessage(EventId = 1012, EventName = "HidDriverRegistrationFailure", Level = LogLevel.Error, Message = "Failed to register an instance of \"{TypeName}\" from \"{AssemblyName}\" for the devices \"{DeviceNames}\".")]
	public static partial void HidDriverRegistrationFailure(this ILogger logger, string typeName, string assemblyName, IEnumerable<string> deviceNames, Exception exception);
}
