using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Exo.Features.Monitors;
using CoolerType = Exo.Cooling.CoolerType;
using DeviceId = DeviceTools.DeviceId;
using DeviceIdSource = DeviceTools.DeviceIdSource;
using GrpcCoolerInformation = Exo.Contracts.Ui.Settings.CoolerInformation;
using GrpcCoolerPowerLimits = Exo.Contracts.Ui.Settings.CoolerPowerLimits;
using GrpcCoolerType = Exo.Contracts.Ui.Settings.CoolerType;
using GrpcCoolingDeviceInformation = Exo.Contracts.Ui.Settings.CoolingDeviceInformation;
using GrpcCoolingModes = Exo.Contracts.Ui.Settings.CoolingModes;
using GrpcDeviceId = Exo.Contracts.Ui.Settings.DeviceId;
using GrpcDeviceIdSource = Exo.Contracts.Ui.Settings.DeviceIdSource;
using GrpcDeviceInformation = Exo.Contracts.Ui.Settings.DeviceInformation;
using GrpcLightingZoneInformation = Exo.Contracts.Ui.Settings.LightingZoneInformation;
using GrpcMetadataArchiveCategory = Exo.Contracts.Ui.Settings.MetadataArchiveCategory;
using GrpcMonitorInformation = Exo.Contracts.Ui.Settings.MonitorInformation;
using GrpcMonitorSetting = Exo.Contracts.Ui.Settings.MonitorSetting;
using GrpcNonContinuousValue = Exo.Contracts.Ui.Settings.NonContinuousValue;
using GrpcSensorDataType = Exo.Contracts.Ui.Settings.SensorDataType;
using GrpcSensorDeviceInformation = Exo.Contracts.Ui.Settings.SensorDeviceInformation;
using GrpcSensorInformation = Exo.Contracts.Ui.Settings.SensorInformation;
using GrpcVendorIdSource = Exo.Contracts.Ui.Settings.VendorIdSource;
using GrpcWatchNotificationKind = Exo.Contracts.Ui.WatchNotificationKind;
using VendorIdSource = DeviceTools.VendorIdSource;

namespace Exo.Service.Grpc;

internal static class GrpcConvert
{
	public static GrpcDeviceInformation ToGrpc(this DeviceStateInformation deviceInformation)
		=> new()
		{
			Id = deviceInformation.Id,
			FriendlyName = deviceInformation.FriendlyName,
			Category = (Exo.Contracts.Ui.Settings.DeviceCategory)deviceInformation.Category,
			FeatureIds = deviceInformation.FeatureIds,
			DeviceIds = ImmutableArray.CreateRange(deviceInformation.DeviceIds, id => id.ToGrpc()),
			MainDeviceIdIndex = deviceInformation.MainDeviceIdIndex,
			SerialNumber = deviceInformation.SerialNumber,
			IsAvailable = deviceInformation.IsAvailable,
		};

	public static GrpcLightingZoneInformation ToGrpc(this LightingZoneInformation zoneInformation)
		=> new()
		{
			ZoneId = zoneInformation.ZoneId,
			SupportedEffectIds = ImmutableCollectionsMarshal.AsImmutableArray
			(
				Array.ConvertAll
				(
					ImmutableCollectionsMarshal.AsArray(zoneInformation.SupportedEffectTypes)!,
					t => EffectSerializer.GetEffectInformation(t).EffectId
				)
			),
		};

	public static GrpcSensorDeviceInformation ToGrpc(this SensorDeviceInformation sensorDeviceInformation)
		=> new()
		{
			DeviceId = sensorDeviceInformation.DeviceId,
			Sensors = ImmutableArray.CreateRange(sensorDeviceInformation.Sensors, ToGrpc),
		};

	public static GrpcSensorInformation ToGrpc(this SensorInformation sensorInformation)
		=> new()
		{
			SensorId = sensorInformation.SensorId,
			DataType = sensorInformation.DataType.ToGrpc(),
			Unit = sensorInformation.Unit,
			IsPolled = sensorInformation.IsPolled,
			ScaleMinimumValue = sensorInformation.ScaleMinimumValue is not null ? Convert.ToDouble(sensorInformation.ScaleMinimumValue) : null,
			ScaleMaximumValue = sensorInformation.ScaleMaximumValue is not null ? Convert.ToDouble(sensorInformation.ScaleMaximumValue) : null,
		};

	public static GrpcCoolingDeviceInformation ToGrpc(this CoolingDeviceInformation coolingDeviceInformation)
		=> new()
		{
			DeviceId = coolingDeviceInformation.DeviceId,
			Coolers = ImmutableArray.CreateRange(coolingDeviceInformation.Coolers, ToGrpc),
		};

	public static GrpcCoolerInformation ToGrpc(this CoolerInformation coolerInformation)
		=> new()
		{
			CoolerId = coolerInformation.CoolerId,
			SpeedSensorId = coolerInformation.SpeedSensorId,
			Type = coolerInformation.Type.ToGrpc(),
			SupportedCoolingModes = coolerInformation.SupportedCoolingModes.ToGrpc(),
			PowerLimits = coolerInformation.PowerLimits is { } powerLimits ?
				powerLimits.ToGrpc() :
				null,
		};

	public static GrpcCoolerType ToGrpc(this CoolerType coolerType)
		=> coolerType switch
		{
			CoolerType.Other => GrpcCoolerType.Other,
			CoolerType.Fan => GrpcCoolerType.Fan,
			CoolerType.Pump => GrpcCoolerType.Pump,
			_ => GrpcCoolerType.Other,
		};

	public static GrpcCoolingModes ToGrpc(this CoolingModes coolingModes) => (GrpcCoolingModes)(int)coolingModes;

	public static GrpcCoolerPowerLimits ToGrpc(this CoolerPowerLimits powerLimits)
		=> new()
		{
			MinimumPower = powerLimits.MinimumPower,
			CanSwitchOff = powerLimits.CanSwitchOff,
		};

	public static GrpcWatchNotificationKind ToGrpc(this WatchNotificationKind notificationKind)
		=> notificationKind switch
		{
			WatchNotificationKind.Enumeration => GrpcWatchNotificationKind.Enumeration,
			WatchNotificationKind.Addition => GrpcWatchNotificationKind.Addition,
			WatchNotificationKind.Removal => GrpcWatchNotificationKind.Removal,
			WatchNotificationKind.Update => GrpcWatchNotificationKind.Update,
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
			DeviceIdSource.PlugAndPlay => GrpcDeviceIdSource.PlugAndPlay,
			DeviceIdSource.Display => GrpcDeviceIdSource.Display,
			DeviceIdSource.Pci => GrpcDeviceIdSource.Pci,
			DeviceIdSource.Usb => GrpcDeviceIdSource.Usb,
			DeviceIdSource.Bluetooth => GrpcDeviceIdSource.Bluetooth,
			DeviceIdSource.BluetoothLowEnergy => GrpcDeviceIdSource.BluetoothLowEnergy,
			DeviceIdSource.EQuad => GrpcDeviceIdSource.EQuad,
			_ => throw new NotImplementedException()
		};

	public static GrpcVendorIdSource ToGrpc(this VendorIdSource deviceIdSource)
		=> deviceIdSource switch
		{
			VendorIdSource.Unknown => GrpcVendorIdSource.Unknown,
			VendorIdSource.PlugAndPlay => GrpcVendorIdSource.PlugAndPlay,
			VendorIdSource.Pci => GrpcVendorIdSource.Pci,
			VendorIdSource.Usb => GrpcVendorIdSource.Usb,
			VendorIdSource.Bluetooth => GrpcVendorIdSource.Bluetooth,
			_ => throw new NotImplementedException()
		};

	public static GrpcSensorDataType ToGrpc(this SensorDataType dataType)
		=> dataType switch
		{
			SensorDataType.UInt8 => GrpcSensorDataType.UInt8,
			SensorDataType.UInt16 => GrpcSensorDataType.UInt16,
			SensorDataType.UInt32 => GrpcSensorDataType.UInt32,
			SensorDataType.UInt64 => GrpcSensorDataType.UInt64,
			SensorDataType.UInt128 => GrpcSensorDataType.UInt128,
			SensorDataType.SInt8 => GrpcSensorDataType.SInt8,
			SensorDataType.SInt16 => GrpcSensorDataType.SInt16,
			SensorDataType.SInt32 => GrpcSensorDataType.SInt32,
			SensorDataType.SInt64 => GrpcSensorDataType.SInt64,
			SensorDataType.SInt128 => GrpcSensorDataType.SInt128,
			SensorDataType.Float16 => GrpcSensorDataType.Float16,
			SensorDataType.Float32 => GrpcSensorDataType.Float32,
			SensorDataType.Float64 => GrpcSensorDataType.Float64,
			_ => throw new NotImplementedException()
		};

	public static GrpcMonitorInformation ToGrpc(this MonitorInformation information)
		=> new()
		{
			DeviceId = information.DeviceId,
			SupportedSettings = ImmutableArray.CreateRange(information.SupportedSettings, ToGrpc),
			InputSelectSources = information.InputSelectSources.IsDefaultOrEmpty ? [] : ImmutableArray.CreateRange(information.InputSelectSources, ToGrpc),
		};

	public static GrpcMonitorSetting ToGrpc(this MonitorSetting setting)
		=> setting switch
		{
			MonitorSetting.Unknown => GrpcMonitorSetting.Unknown,
			MonitorSetting.Brightness => GrpcMonitorSetting.Brightness,
			MonitorSetting.Contrast => GrpcMonitorSetting.Contrast,
			MonitorSetting.AudioVolume => GrpcMonitorSetting.AudioVolume,
			MonitorSetting.InputSelect => GrpcMonitorSetting.InputSelect,
			MonitorSetting.VideoGainRed => GrpcMonitorSetting.VideoGainRed,
			MonitorSetting.VideoGainGreen => GrpcMonitorSetting.VideoGainGreen,
			MonitorSetting.VideoGainBlue => GrpcMonitorSetting.VideoGainBlue,
			MonitorSetting.SixAxisSaturationControlRed => GrpcMonitorSetting.SixAxisSaturationControlRed,
			MonitorSetting.SixAxisSaturationControlYellow => GrpcMonitorSetting.SixAxisSaturationControlYellow,
			MonitorSetting.SixAxisSaturationControlGreen => GrpcMonitorSetting.SixAxisSaturationControlGreen,
			MonitorSetting.SixAxisSaturationControlCyan => GrpcMonitorSetting.SixAxisSaturationControlCyan,
			MonitorSetting.SixAxisSaturationControlBlue => GrpcMonitorSetting.SixAxisSaturationControlBlue,
			MonitorSetting.SixAxisSaturationControlMagenta => GrpcMonitorSetting.SixAxisSaturationControlMagenta,
			MonitorSetting.SixAxisHueControlRed => GrpcMonitorSetting.SixAxisHueControlRed,
			MonitorSetting.SixAxisHueControlYellow => GrpcMonitorSetting.SixAxisHueControlYellow,
			MonitorSetting.SixAxisHueControlGreen => GrpcMonitorSetting.SixAxisHueControlGreen,
			MonitorSetting.SixAxisHueControlCyan => GrpcMonitorSetting.SixAxisHueControlCyan,
			MonitorSetting.SixAxisHueControlBlue => GrpcMonitorSetting.SixAxisHueControlBlue,
			MonitorSetting.SixAxisHueControlMagenta => GrpcMonitorSetting.SixAxisHueControlMagenta,
			_ => throw new NotImplementedException()
		};

	public static GrpcNonContinuousValue ToGrpc(this NonContinuousValueDescription value)
		=> new()
		{
			Value = value.Value,
			NameStringId = value.NameStringId != default ? value.NameStringId : null,
			CustomName = value.CustomName,
		};

	public static MonitorSetting FromGrpc(this GrpcMonitorSetting setting)
		=> setting switch
		{
			GrpcMonitorSetting.Unknown => MonitorSetting.Unknown,
			GrpcMonitorSetting.Brightness => MonitorSetting.Brightness,
			GrpcMonitorSetting.Contrast => MonitorSetting.Contrast,
			GrpcMonitorSetting.AudioVolume => MonitorSetting.AudioVolume,
			GrpcMonitorSetting.InputSelect => MonitorSetting.InputSelect,
			GrpcMonitorSetting.VideoGainRed => MonitorSetting.VideoGainRed,
			GrpcMonitorSetting.VideoGainGreen => MonitorSetting.VideoGainGreen,
			GrpcMonitorSetting.VideoGainBlue => MonitorSetting.VideoGainBlue,
			GrpcMonitorSetting.SixAxisSaturationControlRed => MonitorSetting.SixAxisSaturationControlRed,
			GrpcMonitorSetting.SixAxisSaturationControlYellow => MonitorSetting.SixAxisSaturationControlYellow,
			GrpcMonitorSetting.SixAxisSaturationControlGreen => MonitorSetting.SixAxisSaturationControlGreen,
			GrpcMonitorSetting.SixAxisSaturationControlCyan => MonitorSetting.SixAxisSaturationControlCyan,
			GrpcMonitorSetting.SixAxisSaturationControlBlue => MonitorSetting.SixAxisSaturationControlBlue,
			GrpcMonitorSetting.SixAxisSaturationControlMagenta => MonitorSetting.SixAxisSaturationControlMagenta,
			GrpcMonitorSetting.SixAxisHueControlRed => MonitorSetting.SixAxisHueControlRed,
			GrpcMonitorSetting.SixAxisHueControlYellow => MonitorSetting.SixAxisHueControlYellow,
			GrpcMonitorSetting.SixAxisHueControlGreen => MonitorSetting.SixAxisHueControlGreen,
			GrpcMonitorSetting.SixAxisHueControlCyan => MonitorSetting.SixAxisHueControlCyan,
			GrpcMonitorSetting.SixAxisHueControlBlue => MonitorSetting.SixAxisHueControlBlue,
			GrpcMonitorSetting.SixAxisHueControlMagenta => MonitorSetting.SixAxisHueControlMagenta,
			_ => throw new NotImplementedException()
		};

	public static GrpcMetadataArchiveCategory ToGrpc(this MetadataArchiveCategory category)
		=> category switch
		{
			MetadataArchiveCategory.Strings => GrpcMetadataArchiveCategory.Strings,
			MetadataArchiveCategory.LightingEffects => GrpcMetadataArchiveCategory.LightingEffects,
			MetadataArchiveCategory.LightingZones => GrpcMetadataArchiveCategory.LightingZones,
			MetadataArchiveCategory.Sensors => GrpcMetadataArchiveCategory.Sensors,
			MetadataArchiveCategory.Coolers => GrpcMetadataArchiveCategory.Coolers,
			_ => throw new NotImplementedException()
		};
}
