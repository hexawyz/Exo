using Microsoft.Extensions.Logging;

namespace Exo.Devices.NVidia;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 10001, EventName = "NvApiVersion", Level = LogLevel.Information, Message = "NVAPI Version is {NvApiVersion}.")]
	public static partial void NvApiVersion(this ILogger logger, string nvApiVersion);

	[LoggerMessage(EventId = 10101,
		EventName = "IlluminationZoneNotMapped",
		Level = LogLevel.Warning,
		Message = "Illumination zone #{IlluminationZoneIndex} located at #{IlluminationZoneLocationIndex} for component {IlluminationZoneLocationComponent} {IlluminationZoneLocationFace} is not mapped by the driver.")]
	public static partial void IlluminationZoneNotMapped
	(
		this ILogger logger,
		byte illuminationZoneIndex,
		NvApi.Gpu.Client.IlluminationZoneLocationComponent illuminationZoneLocationComponent,
		NvApi.Gpu.Client.IlluminationZoneLocationFace illuminationZoneLocationFace,
		byte illuminationZoneLocationIndex
	);

	[LoggerMessage(EventId = 10102,
		EventName = "IlluminationZoneInvalidType",
		Level = LogLevel.Error,
		Message = "Illumination zone #{IlluminationZoneIndex} is invalid.")]
	public static partial void IlluminationZoneInvalidType
	(
		this ILogger logger,
		byte illuminationZoneIndex
	);

	[LoggerMessage(EventId = 10103,
		EventName = "IlluminationZoneUnknownType",
		Level = LogLevel.Error,
		Message = "Illumination zone #{IlluminationZoneIndex} type is unknown.")]
	public static partial void IlluminationZoneUnknownType
	(
		this ILogger logger,
		byte illuminationZoneIndex
	);

	[LoggerMessage(EventId = 10201,
		EventName = "GpuClockNotSupported",
		Level = LogLevel.Warning,
		Message = "Clock {Clock} is not supported. It will not be exposed as a sensor.")]
	public static partial void GpuClockNotSupported
	(
		this ILogger logger,
		NvApi.Gpu.PublicClock clock
	);
}
