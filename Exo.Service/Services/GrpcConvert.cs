using System;
using GrpcDeviceInformation = Exo.Ui.Contracts.DeviceInformation;
using GrpcLightingZoneInformation = Exo.Ui.Contracts.LightingZoneInformation;
using GrpcWatchNotificationKind = Exo.Ui.Contracts.WatchNotificationKind;
using GrpcDeviceId = Exo.Ui.Contracts.DeviceId;
using GrpcDeviceIdSource = Exo.Ui.Contracts.DeviceIdSource;
using GrpcVendorIdSource = Exo.Ui.Contracts.VendorIdSource;
using DeviceTools;

namespace Exo.Service.Services;

internal static class GrpcConvert
{
	public static GrpcDeviceInformation ToGrpc(this DeviceInformation deviceInformation)
		=> new()
		{
			Id = deviceInformation.Id,
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

	public static GrpcDeviceId ToGrpc(this DeviceId deviceId)
		=> new()
		{
			VendorIdSource = deviceId.VendorIdSource.ToGrpc(),
			Source = deviceId.Source.ToGrpc(),
			VendorId = deviceId.VendorId,
			ProductId = deviceId.ProductId,
			Version = deviceId.Version,
		};

	public static GrpcDeviceIdSource ToGrpc(this DeviceIdSource deviceIdSource)
		=> deviceIdSource switch
		{
			DeviceIdSource.Unknown => GrpcDeviceIdSource.Unknown,
			DeviceIdSource.Pci => GrpcDeviceIdSource.Pci,
			DeviceIdSource.Usb => GrpcDeviceIdSource.Usb,
			DeviceIdSource.Bluetooth => GrpcDeviceIdSource.Bluetooth,
			DeviceIdSource.BluetoothLowEnergy => GrpcDeviceIdSource.BluetoothLowEnergy,
			_ => throw new NotImplementedException()
		};

	public static GrpcVendorIdSource ToGrpc(this VendorIdSource deviceIdSource)
		=> deviceIdSource switch
		{
			VendorIdSource.Unknown => GrpcVendorIdSource.Unknown,
			VendorIdSource.Pci => GrpcVendorIdSource.Pci,
			VendorIdSource.Usb => GrpcVendorIdSource.Usb,
			VendorIdSource.Bluetooth => GrpcVendorIdSource.Bluetooth,
			_ => throw new NotImplementedException()
		};
}
