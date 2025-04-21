using Microsoft.Extensions.Logging;

namespace Exo.Devices.NVidia;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Information, Message = "NVAPI Version is {NvApiVersion}.")]
	public static partial void NvApiVersion
	(
		this ILogger logger,
		string nvApiVersion
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to retrieve the EDID for display {DisplayId:X8} of output {OutputId:X8} for GPU {DeviceFriendlyName}.")]
	public static partial void EdidRetrievalFailure
	(
		this ILogger logger,
		uint displayId,
		uint outputId,
		string deviceFriendlyName,
		Exception exception
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse the EDID for display {DisplayId:X8} of output {OutputId:X8} for GPU {DeviceFriendlyName}.")]
	public static partial void EdidParsingFailure
	(
		this ILogger logger,
		uint displayId,
		uint outputId,
		string deviceFriendlyName,
		Exception exception
	);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Illumination zone #{IlluminationZoneIndex} located at #{IlluminationZoneLocationIndex} for component {IlluminationZoneLocationComponent} {IlluminationZoneLocationFace} is not mapped by the driver.")]
	public static partial void IlluminationZoneNotMapped
	(
		this ILogger logger,
		byte illuminationZoneIndex,
		NvApi.Gpu.Client.IlluminationZoneLocationComponent illuminationZoneLocationComponent,
		NvApi.Gpu.Client.IlluminationZoneLocationFace illuminationZoneLocationFace,
		byte illuminationZoneLocationIndex
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Illumination zone #{IlluminationZoneIndex} is invalid.")]
	public static partial void IlluminationZoneInvalidType
	(
		this ILogger logger,
		byte illuminationZoneIndex
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Illumination zone #{IlluminationZoneIndex} type is unknown.")]
	public static partial void IlluminationZoneUnknownType
	(
		this ILogger logger,
		byte illuminationZoneIndex
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to query the clock frequencies for GPU {DeviceFriendlyName}.")]
	public static partial void GpuClockFrequenciesQueryFailure
	(
		this ILogger logger,
		string deviceFriendlyName,
		Exception exception
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to query the dynamic PStates for GPU {DeviceFriendlyName}.")]
	public static partial void GpuDynamicPStatesQueryFailure
	(
		this ILogger logger,
		string deviceFriendlyName,
		Exception exception
	);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Clock {Clock} is not supported. It will not be exposed as a sensor.")]
	public static partial void GpuClockNotSupported
	(
		this ILogger logger,
		NvApi.Gpu.PublicClock clock
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "An error occurred while watching utilization for GPU {DeviceFriendlyName}.")]
	public static partial void GpuUtilizationWatchingFailure
	(
		this ILogger logger,
		string deviceFriendlyName,
		Exception exception
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to query the status of fan coolers for GPU {DeviceFriendlyName}.")]
	public static partial void GpuFanCoolerStatusQueryFailure
	(
		this ILogger logger,
		string deviceFriendlyName,
		Exception exception
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "Failed to query the thermal settings for GPU {DeviceFriendlyName}.")]
	public static partial void GpuThermalSettingsQueryFailure
	(
		this ILogger logger,
		string deviceFriendlyName,
		Exception exception
	);
}
