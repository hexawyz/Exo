using Microsoft.Extensions.Logging;

namespace Exo.Devices.Asus.Aura;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Device detected at SMBus address {SmBusDeviceAddress:X2}.")]
	public static partial void SmBusDeviceDetected(this ILogger logger, byte smBusDeviceAddress);

	[LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Aura device detected at SMBus address {SmBusDeviceAddress:X2}.")]
	public static partial void SmBusAuraDeviceDetected(this ILogger logger, byte smBusDeviceAddress);

	[LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Aura device at SMBus address {SmBusDeviceAddress:X2} is memory module {MemorySlotIndex}.")]
	public static partial void SmBusAuraDeviceSlotDetected(this ILogger logger, byte smBusDeviceAddress, byte memorySlotIndex);

	[LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "One or more Aura devices detected at default SMBus address for Aura devices.")]
	public static partial void SmBusAuraDeviceDetectedAtDefaultAddress(this ILogger logger);

	[LoggerMessage(EventId = 1005, Level = LogLevel.Debug, Message = "No Aura device detected at default SMBus address for Aura devices.")]
	public static partial void SmBusAuraDeviceNotDetectedAtDefaultAddress(this ILogger logger);

	[LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Aura device at default SMBus address moved to SMBus address {SmBusDeviceAddress:X2}.")]
	public static partial void SmBusAuraDeviceRemapped(this ILogger logger, byte smBusDeviceAddress);

	[LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Failed to move device at default SMBus address to SMBus address {SmBusDeviceAddress:X2}.")]
	public static partial void SmBusAuraDeviceRemappingFailure(this ILogger logger, byte smBusDeviceAddress);
}
