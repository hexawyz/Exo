using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Error, Message = "The Kraken device at {DeviceName} is locked by another software. The image upload feature will be disabled.")]
	public static partial void KrakenWinUsbDeviceLocked
	(
		this ILogger logger,
		string deviceName
	);

	[LoggerMessage(Level = LogLevel.Information, Message = "Kraken device at {DeviceName} lighting accessory {LightingAccessoryNumber} of channel {LightingChannelNumber} is {LightingAccessoryName} (ID {LightingAccessoryId:X2}).")]
	public static partial void KrakenLightingKnownAccessory
	(
		this ILogger logger,
		string deviceName,
		byte lightingChannelNumber,
		byte lightingAccessoryNumber,
		byte lightingAccessoryId,
		string lightingAccessoryName
	);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Kraken device at {DeviceName} lighting accessory {LightingAccessoryNumber} of channel {LightingChannelNumber} is unsupported ID {LightingAccessoryId:X2}.")]
	public static partial void KrakenLightingUnknownAccessory
	(
		this ILogger logger,
		string deviceName,
		byte lightingChannelNumber,
		byte lightingAccessoryNumber,
		byte lightingAccessoryId
	);
}
