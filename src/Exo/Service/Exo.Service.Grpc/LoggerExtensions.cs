using Exo.Features;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal static partial class LoggerExtensions
{
	// TODO: Reorganize Event IDs (They are currently not used but the plan is to log events to the event log at some point…)

	[LoggerMessage(EventId = 10_101, EventName = "GrpcDeviceServiceWatchStart", Level = LogLevel.Debug, Message = "Started watching devices.")]
	public static partial void GrpcDeviceServiceWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_102, EventName = "GrpcDeviceServiceWatchStop", Level = LogLevel.Debug, Message = "Stopped watching devices.")]
	public static partial void GrpcDeviceServiceWatchStop(this ILogger logger);

	// These should have more sensical IDs and replace all the other specialized messages.
	[LoggerMessage(EventId = 10_901, EventName = "GrpcSpecializedDeviceServiceWatchStart", Level = LogLevel.Debug, Message = "Started watching {DeviceService} devices.")]
	public static partial void GrpcSpecializedDeviceServiceWatchStart(this ILogger logger, GrpcService deviceService);

	[LoggerMessage(EventId = 10_902, EventName = "GrpcSpecializedDeviceServiceWatchStop", Level = LogLevel.Debug, Message = "Stopped watching {DeviceService} devices.")]
	public static partial void GrpcSpecializedDeviceServiceWatchStop(this ILogger logger, GrpcService deviceService);


	[LoggerMessage(EventId = 10_103, EventName = "GrpcDeviceServiceWatchNotification", Level = LogLevel.Trace, Message = "Device notification: {NotificationKind} for {DeviceId} ({DeviceFriendlyName}). Availability: {IsAvailable}.")]
	public static partial void GrpcDeviceServiceWatchNotification(this ILogger logger, WatchNotificationKind notificationKind, Guid deviceId, string deviceFriendlyName, bool isAvailable);

	[LoggerMessage(EventId = 10_201, EventName = "GrpcBatteryServiceWatchStart", Level = LogLevel.Debug, Message = "Started watching battery levels.")]
	public static partial void GrpcBatteryServiceWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_202, EventName = "GrpcBatteryServiceWatchStop", Level = LogLevel.Debug, Message = "Stopped watching battery levels.")]
	public static partial void GrpcBatteryServiceWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_203, EventName = "GrpcBatteryServiceWatchNotification", Level = LogLevel.Trace, Message = "Battery notification: {NotificationKind} for {DeviceId}. From level {OldBatteryLevel} to {NewBatteryLevel}. From {OldBatteryStatus} to {NewBatteryStatus}. External power from {OldExternalPowerStatus} to {NewExternalPowerStatus}.")]
	public static partial void GrpcBatteryServiceWatchNotification(this ILogger logger, WatchNotificationKind notificationKind, Guid deviceId, float? oldBatteryLevel, float? newBatteryLevel, BatteryStatus oldBatteryStatus, BatteryStatus newBatteryStatus, ExternalPowerStatus oldExternalPowerStatus, ExternalPowerStatus newExternalPowerStatus);

	[LoggerMessage(EventId = 10_301, EventName = "GrpcSensorServiceDeviceWatchStart", Level = LogLevel.Debug, Message = "Started watching sensor devices.")]
	public static partial void GrpcSensorServiceDeviceWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_302, EventName = "GrpcSensorServiceDeviceWatchStop", Level = LogLevel.Debug, Message = "Stopped watching sensor devices.")]
	public static partial void GrpcSensorServiceDeviceWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_401, EventName = "GrpcLightingServiceDeviceWatchStart", Level = LogLevel.Debug, Message = "Started watching lighting devices.")]
	public static partial void GrpcLightingServiceDeviceWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_402, EventName = "GrpcLightingServiceDeviceWatchStop", Level = LogLevel.Debug, Message = "Stopped watching lighting devices.")]
	public static partial void GrpcLightingServiceDeviceWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_403, EventName = "GrpcLightingServiceEffectWatchStart", Level = LogLevel.Debug, Message = "Started watching lighting effects.")]
	public static partial void GrpcLightingServiceEffectWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_404, EventName = "GrpcLightingServiceEffectWatchStop", Level = LogLevel.Debug, Message = "Stopped watching lighting effects.")]
	public static partial void GrpcLightingServiceEffectWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_405, EventName = "GrpcLightingServiceBrightnessWatchStart", Level = LogLevel.Debug, Message = "Started watching lighting device brightness changes.")]
	public static partial void GrpcLightingServiceBrightnessWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_406, EventName = "GrpcLightingServiceBrightnessWatchStop", Level = LogLevel.Debug, Message = "Stopped watching lighting device brightness changes.")]
	public static partial void GrpcLightingServiceBrightnessWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_411, EventName = "GrpcLightingServiceEffectInformationRetrievalError", Level = LogLevel.Error, Message = "An error occurred when retrieving informations on effect {EffectType}.")]
	public static partial void GrpcLightingServiceEffectInformationRetrievalError(this ILogger logger, Type effectType, Exception exception);

	[LoggerMessage(EventId = 10_412, EventName = "GrpcLightingServiceEffectApplicationError", Level = LogLevel.Error, Message = "An error occurred when applying the effect {EffectId} on Zone {ZoneId} of device {DeviceId}.")]
	public static partial void GrpcLightingServiceEffectApplicationError(this ILogger logger, Guid deviceId, Guid zoneId, Guid effectId, Exception exception);

	[LoggerMessage(EventId = 10_501, EventName = "GrpcMouseServiceDpiWatchStart", Level = LogLevel.Debug, Message = "Started watching mouse DPI changes.")]
	public static partial void GrpcMouseServiceDpiWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_502, EventName = "GrpcMouseServiceDpiWatchStop", Level = LogLevel.Debug, Message = "Stopped watching mouse DPI changes.")]
	public static partial void GrpcMouseServiceDpiWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_601, EventName = "GrpcMonitorServiceSettingWatchStart", Level = LogLevel.Debug, Message = "Started watching monitor setting changes.")]
	public static partial void GrpcMonitorServiceSettingWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_602, EventName = "GrpcMonitorServiceSettingWatchStop", Level = LogLevel.Debug, Message = "Stopped watching monitor setting changes.")]
	public static partial void GrpcMonitorServiceSettingWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_701, EventName = "GrpcCoolingServiceDeviceWatchStart", Level = LogLevel.Debug, Message = "Started watching cooling devices.")]
	public static partial void GrpcCoolingServiceDeviceWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_702, EventName = "GrpcCoolingServiceDeviceWatchStop", Level = LogLevel.Debug, Message = "Stopped watching cooling devices.")]
	public static partial void GrpcCoolingServiceDeviceWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_703, EventName = "GrpcCoolingServiceChangeWatchStart", Level = LogLevel.Debug, Message = "Started watching cooling changes.")]
	public static partial void GrpcCoolingServiceChangeWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_704, EventName = "GrpcCoolingServiceChangeWatchStop", Level = LogLevel.Debug, Message = "Stopped watching cooling changes.")]
	public static partial void GrpcCoolingServiceChangeWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_801, EventName = "GrpcImageServiceImageWatchStart", Level = LogLevel.Debug, Message = "Started watching image changes.")]
	public static partial void GrpcImageServiceImageWatchStart(this ILogger logger);

	[LoggerMessage(EventId = 10_802, EventName = "GrpcImageServiceImageWatchStop", Level = LogLevel.Debug, Message = "Stopped watching image changes.")]
	public static partial void GrpcImageServiceImageWatchStop(this ILogger logger);

	[LoggerMessage(EventId = 10_803, EventName = "GrpcImageServiceAddImageRequestStart", Level = LogLevel.Information, Message = "Start of request to add image {ImageName} of {imageDataLength} bytes.")]
	public static partial void GrpcImageServiceAddImageRequestStart(this ILogger logger, string imageName, uint imageDataLength);

	[LoggerMessage(EventId = 10_804, EventName = "GrpcImageServiceAddImageRequestWriteComplete", Level = LogLevel.Debug, Message = "Notification of image data written for request to add image {ImageName}.")]
	public static partial void GrpcImageServiceAddImageRequestWriteComplete(this ILogger logger, string imageName);

	[LoggerMessage(EventId = 10_805, EventName = "GrpcImageServiceAddImageRequestFailure", Level = LogLevel.Error, Message = "Failure to process request to add image {ImageName}.")]
	public static partial void GrpcImageServiceAddImageRequestFailure(this ILogger logger, string imageName, Exception exception);

	[LoggerMessage(EventId = 10_806, EventName = "GrpcImageServiceAddImageRequestSuccess", Level = LogLevel.Information, Message = "Success of request to add image {ImageName}.")]
	public static partial void GrpcImageServiceAddImageRequestSuccess(this ILogger logger, string imageName);
}

public enum GrpcService
{
	Device = 0,
	Power = 1,
	Battery = 2,
	Lighting = 3,
	EmbeddedMonitor = 4,
	Sensor = 5,
	Cooling = 6,
	Light = 7,
}
