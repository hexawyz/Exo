using System;
using Exo.Contracts;
using GrpcDeviceInformation = Exo.Ui.Contracts.DeviceInformation;
using GrpcLightingZoneInformation = Exo.Ui.Contracts.LightingZoneInformation;
using GrpcWatchNotificationKind = Exo.Ui.Contracts.WatchNotificationKind;

namespace Exo.Service.Services;

internal static class GrpcConvert
{
	public static GrpcDeviceInformation ToGrpc(this DeviceInformation deviceInformation)
		=> new()
		{
			DeviceId = deviceInformation.DeviceId,
			FriendlyName = deviceInformation.FriendlyName,
			Category = (Exo.Ui.Contracts.DeviceCategory)deviceInformation.Category,
			DriverTypeName = deviceInformation.DriverType.ToString(),
			FeatureTypeNames = Array.ConvertAll(deviceInformation.FeatureTypes, t => t.ToString()).AsImmutable(),
		};

	public static GrpcLightingZoneInformation ToGrpc(this LightingZoneInformation zoneInformation)
		=> new()
		{
			ZoneId = zoneInformation.ZoneId,
			SupportedEffectIds = Array.ConvertAll(zoneInformation.SupportedEffectTypes.AsMutable(), t => EffectSerializer.GetEffectInformation(t).EffectId).AsImmutable(),
		};

	public static GrpcWatchNotificationKind ToGrpc(this WatchNotificationKind notificationKind)
		=> notificationKind switch
		{
			WatchNotificationKind.Enumeration => GrpcWatchNotificationKind.Enumeration,
			WatchNotificationKind.Addition => GrpcWatchNotificationKind.Arrival,
			WatchNotificationKind.Removal => GrpcWatchNotificationKind.Removal,
			_ => throw new NotImplementedException()
		};
}
