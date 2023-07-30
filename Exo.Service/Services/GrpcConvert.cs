using System;
using GrpcDeviceInformation = Exo.Ui.Contracts.DeviceInformation;
using GrpcLightingZoneInformation = Exo.Ui.Contracts.LightingZoneInformation;
using GrpcWatchNotificationKind = Exo.Ui.Contracts.WatchNotificationKind;

namespace Exo.Service.Services;

internal static class GrpcConvert
{
	public static GrpcDeviceInformation ToGrpc(this DeviceInformation deviceInformation)
		=> new()
		{
			UniqueId = deviceInformation.UniqueId,
			FriendlyName = deviceInformation.FriendlyName,
			Category = (Exo.Ui.Contracts.DeviceCategory)deviceInformation.Category,
			DriverTypeName = deviceInformation.DriverType.ToString(),
			FeatureTypeNames = Array.ConvertAll(deviceInformation.FeatureTypes, t => t.ToString()).AsImmutable(),
		};

	public static GrpcLightingZoneInformation ToGrpc(this LightingZoneInformation zoneInformation)
		=> new()
		{
			ZoneId = zoneInformation.ZoneId,
			SupportedEffectTypeNames = Array.ConvertAll(zoneInformation.SupportedEffectTypes.AsMutable(), t => t.ToString()).AsImmutable(),
		};

	public static GrpcWatchNotificationKind ToGrpc(this WatchNotificationKind notificationKind)
		=> notificationKind switch
		{
			WatchNotificationKind.Enumeration => Ui.Contracts.WatchNotificationKind.Enumeration,
			WatchNotificationKind.Addition => Ui.Contracts.WatchNotificationKind.Arrival,
			WatchNotificationKind.Removal => Ui.Contracts.WatchNotificationKind.Removal,
			_ => throw new NotImplementedException()
		};
}
