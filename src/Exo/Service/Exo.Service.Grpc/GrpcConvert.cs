using System.Collections.Immutable;
using Exo.Features.Monitors;
using Exo.Lighting;
using CoolerType = Exo.Cooling.CoolerType;
using DeviceId = DeviceTools.DeviceId;
using DeviceIdSource = DeviceTools.DeviceIdSource;
using GrpcCoolerInformation = Exo.Contracts.Ui.Settings.CoolerInformation;
using GrpcCoolerPowerLimits = Exo.Contracts.Ui.Settings.CoolerPowerLimits;
using GrpcCoolerType = Exo.Contracts.Ui.Settings.CoolerType;
using GrpcCoolingDeviceInformation = Exo.Contracts.Ui.Settings.CoolingDeviceInformation;
using GrpcCoolingParameters = Exo.Contracts.Ui.Settings.Cooling.CoolingParameters;
using GrpcCoolingControlCurve = Exo.Contracts.Ui.Settings.Cooling.CoolingControlCurve;
using GrpcCoolingModes = Exo.Contracts.Ui.Settings.CoolingModes;
using GrpcDeviceId = Exo.Contracts.Ui.Settings.DeviceId;
using GrpcDeviceIdSource = Exo.Contracts.Ui.Settings.DeviceIdSource;
using GrpcDeviceInformation = Exo.Contracts.Ui.Settings.DeviceInformation;
using GrpcDotsPerInch = Exo.Contracts.Ui.Settings.DotsPerInch;
using GrpcLightingPersistenceMode = Exo.Contracts.Ui.Settings.LightingPersistenceMode;
using GrpcLightingZoneInformation = Exo.Contracts.Ui.Settings.LightingZoneInformation;
using GrpcMetadataArchiveCategory = Exo.Contracts.Ui.Settings.MetadataArchiveCategory;
using GrpcMonitorInformation = Exo.Contracts.Ui.Settings.MonitorInformation;
using GrpcMonitorSetting = Exo.Contracts.Ui.Settings.MonitorSetting;
using GrpcMouseDeviceInformation = Exo.Contracts.Ui.Settings.MouseDeviceInformation;
using GrpcMouseDpiPresets = Exo.Contracts.Ui.Settings.MouseDpiPresets;
using GrpcMousePollingFrequencyUpdate = Exo.Contracts.Ui.Settings.MousePollingFrequencyUpdate;
using GrpcNonContinuousValue = Exo.Contracts.Ui.Settings.NonContinuousValue;
using GrpcPowerDeviceInformation = Exo.Contracts.Ui.Settings.PowerDeviceInformation;
using GrpcSensorDataType = Exo.Contracts.Ui.Settings.SensorDataType;
using GrpcSensorCapabilities = Exo.Contracts.Ui.Settings.SensorCapabilities;
using GrpcSensorDeviceInformation = Exo.Contracts.Ui.Settings.SensorDeviceInformation;
using GrpcSensorInformation = Exo.Contracts.Ui.Settings.SensorInformation;
using GrpcVendorIdSource = Exo.Contracts.Ui.Settings.VendorIdSource;
using GrpcWatchNotificationKind = Exo.Contracts.Ui.WatchNotificationKind;
using GrpcImageInformation = Exo.Contracts.Ui.Settings.ImageInformation;
using GrpcImageFormat = Exo.Contracts.Ui.Settings.ImageFormat;
using VendorIdSource = DeviceTools.VendorIdSource;
using Exo.Contracts.Ui.Settings.Cooling;
using Exo.Cooling.Configuration;
using System.Numerics;
using Exo.Images;

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

	public static GrpcPowerDeviceInformation ToGrpc(this PowerDeviceInformation powerDeviceInformation)
		=> new()
		{
			DeviceId = powerDeviceInformation.DeviceId,
			IsConnected = powerDeviceInformation.IsConnected,
			Capabilities = powerDeviceInformation.Capabilities,
			MinimumIdleTime = powerDeviceInformation.MinimumIdleTime,
			MaximumIdleTime = powerDeviceInformation.MaximumIdleTime,
			MinimumBrightness = powerDeviceInformation.MinimumBrightness,
			MaximumBrightness = powerDeviceInformation.MaximumBrightness,
		};

	public static GrpcMouseDeviceInformation ToGrpc(this MouseDeviceInformation mouseDeviceInformation)
		=> new()
		{
			DeviceId = mouseDeviceInformation.DeviceId,
			IsConnected = mouseDeviceInformation.IsConnected,
			Capabilities = mouseDeviceInformation.Capabilities,
			MaximumDpi = mouseDeviceInformation.MaximumDpi.ToGrpc(),
			MinimumDpiPresetCount = mouseDeviceInformation.MinimumDpiPresetCount,
			MaximumDpiPresetCount = mouseDeviceInformation.MaximumDpiPresetCount,
			SupportedPollingFrequencies = mouseDeviceInformation.SupportedPollingFrequencies,
		};

	public static GrpcMouseDpiPresets ToGrpc(this MouseDpiPresetsInformation mouseDpiPresets)
		=> new()
		{
			DeviceId = mouseDpiPresets.DeviceId,
			DpiPresets = ImmutableArray.CreateRange(mouseDpiPresets.DpiPresets, ToGrpc),
		};

	public static GrpcMousePollingFrequencyUpdate ToGrpc(this MousePollingFrequencyNotification mousePollingFrequencyNotification)
		=> new()
		{
			DeviceId = mousePollingFrequencyNotification.DeviceId,
			PollingFrequency = mousePollingFrequencyNotification.PollingFrequency,
		};

	public static GrpcDotsPerInch ToGrpc(this DotsPerInch dpi)
		=> new()
		{
			Horizontal = dpi.Horizontal,
			Vertical = dpi.Vertical,
		};

	public static DotsPerInch FromGrpc(this GrpcDotsPerInch dpi)
		=> new(dpi.Horizontal, dpi.Vertical);

	public static GrpcLightingPersistenceMode ToGrpc(this LightingPersistenceMode persistenceMode)
		=> persistenceMode switch
		{
			LightingPersistenceMode.NeverPersisted => GrpcLightingPersistenceMode.NeverPersisted,
			LightingPersistenceMode.CanPersist => GrpcLightingPersistenceMode.CanPersist,
			LightingPersistenceMode.AlwaysPersisted => GrpcLightingPersistenceMode.AlwaysPersisted,
			_ => throw new NotImplementedException(),
		};

	public static GrpcLightingZoneInformation ToGrpc(this LightingZoneInformation zoneInformation)
		=> new()
		{
			ZoneId = zoneInformation.ZoneId,
			SupportedEffectIds = zoneInformation.SupportedEffectTypeIds,
		};

	public static GrpcSensorDeviceInformation ToGrpc(this SensorDeviceInformation sensorDeviceInformation)
		=> new()
		{
			DeviceId = sensorDeviceInformation.DeviceId,
			Sensors = ImmutableArray.CreateRange(sensorDeviceInformation.Sensors, ToGrpc),
		};

	public static GrpcImageInformation ToGrpc(this ImageInformation imageInformation)
		=> new()
		{
			ImageName = imageInformation.ImageName,
			FileName = imageInformation.FileName,
			Width = imageInformation.Width,
			Height = imageInformation.Height,
			Format = imageInformation.Format.ToGrpc(),
			IsAnimated = imageInformation.IsAnimated,
		};

	public static GrpcSensorInformation ToGrpc(this SensorInformation sensorInformation)
		=> new()
		{
			SensorId = sensorInformation.SensorId,
			DataType = sensorInformation.DataType.ToGrpc(),
			Unit = sensorInformation.Unit,
			Capabilities = sensorInformation.Capabilities.ToGrpc(),
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
			PowerLimits = coolerInformation.PowerLimits is { } powerLimits ?
				powerLimits.ToGrpc() :
				null,
		};

	public static GrpcCoolingParameters ToGrpc(this CoolingUpdate update)
		=> update.CoolingMode switch
		{
			AutomaticCoolingModeConfiguration => new() { Automatic = new() { DeviceId = update.DeviceId, CoolerId = update.CoolerId } },
			FixedCoolingModeConfiguration fixedConfiguration => new() { Fixed = new() { DeviceId = update.DeviceId, CoolerId = update.CoolerId, Power = fixedConfiguration.Power } },
			SoftwareCurveCoolingModeConfiguration softwareCurveConfiguration => new()
			{
				SoftwareControlCurve = new()
				{
					CoolingDeviceId = update.DeviceId,
					CoolerId = update.CoolerId,
					SensorDeviceId = softwareCurveConfiguration.SensorDeviceId,
					SensorId = softwareCurveConfiguration.SensorId,
					FallbackValue = softwareCurveConfiguration.DefaultPower,
					ControlCurve = softwareCurveConfiguration.Curve.ToGrpc(),
				}
			},
			HardwareCurveCoolingModeConfiguration hardwareCurveConfiguration => new()
			{
				HardwareControlCurve = new()
				{
					CoolingDeviceId = update.DeviceId,
					CoolerId = update.CoolerId,
					SensorId = hardwareCurveConfiguration.SensorId,
					ControlCurve = hardwareCurveConfiguration.Curve.ToGrpc(),
				}
			},
			_ => throw new InvalidOperationException()
		};

	private static GrpcCoolingControlCurve ToGrpc(this CoolingControlCurveConfiguration curve)
		=> curve switch
		{
			CoolingControlCurveConfiguration<sbyte> curveSByte => curveSByte.ToGrpcSignedInteger(),
			CoolingControlCurveConfiguration<byte> curveByte => curveByte.ToGrpcUnsignedInteger(),
			CoolingControlCurveConfiguration<short> curveInt16 => curveInt16.ToGrpcSignedInteger(),
			CoolingControlCurveConfiguration<ushort> curveUInt16 => curveUInt16.ToGrpcUnsignedInteger(),
			CoolingControlCurveConfiguration<int> curveInt32 => curveInt32.ToGrpcSignedInteger(),
			CoolingControlCurveConfiguration<uint> curveUInt32 => curveUInt32.ToGrpcUnsignedInteger(),
			CoolingControlCurveConfiguration<long> curveInt64 => curveInt64.ToGrpcSignedInteger(),
			CoolingControlCurveConfiguration<ulong> curveUInt64 => curveUInt64.ToGrpcUnsignedInteger(),
			CoolingControlCurveConfiguration<Half> curveFloat16 => curveFloat16.ToGrpcSinglePrecisionFloatingPoint(),
			CoolingControlCurveConfiguration<float> curveFloat32 => curveFloat32.ToGrpcDoublePrecisionFloatingPoint(),
			CoolingControlCurveConfiguration<double> curveFloat64 => curveFloat64.ToGrpcDoublePrecisionFloatingPoint(),
			_ => throw new InvalidOperationException(),
		};

	private static GrpcCoolingControlCurve ToGrpcUnsignedInteger<TInput>(this CoolingControlCurveConfiguration<TInput> curve)
		where TInput : struct, INumber<TInput>
		=> new()
		{
			UnsignedInteger = new UnsignedIntegerCoolingControlCurve()
			{
				InitialValue = curve.InitialValue,
				SegmentPoints = ImmutableArray.CreateRange(curve.Points, p => new Contracts.Ui.UIntDataPoint { X = ulong.CreateChecked(p.X), Y = p.Y })
			}
		};

	private static GrpcCoolingControlCurve ToGrpcSignedInteger<TInput>(this CoolingControlCurveConfiguration<TInput> curve)
		where TInput : struct, INumber<TInput>
		=> new()
		{
			SignedInteger = new SignedIntegerCoolingControlCurve()
			{
				InitialValue = curve.InitialValue,
				SegmentPoints = ImmutableArray.CreateRange(curve.Points, p => new Contracts.Ui.IntToUIntDataPoint { X = long.CreateChecked(p.X), Y = p.Y })
			}
		};

	private static GrpcCoolingControlCurve ToGrpcSinglePrecisionFloatingPoint<TInput>(this CoolingControlCurveConfiguration<TInput> curve)
		where TInput : struct, INumber<TInput>
		=> new()
		{
			SinglePrecisionFloatingPoint = new SinglePrecisionFloatingPointCoolingControlCurve()
			{
				InitialValue = curve.InitialValue,
				SegmentPoints = ImmutableArray.CreateRange(curve.Points, p => new Contracts.Ui.SingleToUIntDataPoint { X = float.CreateChecked(p.X), Y = p.Y })
			}
		};

	private static GrpcCoolingControlCurve ToGrpcDoublePrecisionFloatingPoint<TInput>(this CoolingControlCurveConfiguration<TInput> curve)
		where TInput : struct, INumber<TInput>
		=> new()
		{
			DoublePrecisionFloatingPoint = new DoublePrecisionFloatingPointCoolingControlCurve()
			{
				InitialValue = curve.InitialValue,
				SegmentPoints = ImmutableArray.CreateRange(curve.Points, p => new Contracts.Ui.DoubleToUIntDataPoint { X = double.CreateChecked(p.X), Y = p.Y })
			}
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

	public static GrpcSensorCapabilities ToGrpc(this SensorCapabilities capabilities)
		=> (GrpcSensorCapabilities)capabilities;

	public static GrpcMonitorInformation ToGrpc(this MonitorInformation information)
		=> new()
		{
			DeviceId = information.DeviceId,
			SupportedSettings = ImmutableArray.CreateRange(information.SupportedSettings, ToGrpc),
			InputSelectSources = information.InputSelectSources.IsDefaultOrEmpty ? [] : ImmutableArray.CreateRange(information.InputSelectSources, ToGrpc),
			InputLagLevels = information.InputLagLevels.IsDefaultOrEmpty ? [] : ImmutableArray.CreateRange(information.InputLagLevels, ToGrpc),
			ResponseTimeLevels = information.ResponseTimeLevels.IsDefaultOrEmpty ? [] : ImmutableArray.CreateRange(information.ResponseTimeLevels, ToGrpc),
			OsdLanguages = information.OsdLanguages.IsDefaultOrEmpty ? [] : ImmutableArray.CreateRange(information.OsdLanguages, ToGrpc),
		};

	public static GrpcMonitorSetting ToGrpc(this MonitorSetting setting)
		=> setting switch
		{
			MonitorSetting.Unknown => GrpcMonitorSetting.Unknown,
			MonitorSetting.Brightness => GrpcMonitorSetting.Brightness,
			MonitorSetting.Contrast => GrpcMonitorSetting.Contrast,
			MonitorSetting.Sharpness => GrpcMonitorSetting.Sharpness,
			MonitorSetting.AudioVolume => GrpcMonitorSetting.AudioVolume,
			MonitorSetting.InputSelect => GrpcMonitorSetting.InputSelect,
			MonitorSetting.VideoGainRed => GrpcMonitorSetting.VideoGainRed,
			MonitorSetting.VideoGainGreen => GrpcMonitorSetting.VideoGainGreen,
			MonitorSetting.VideoGainBlue => GrpcMonitorSetting.VideoGainBlue,
			MonitorSetting.VideoBlackLevelRed => GrpcMonitorSetting.VideoBlackLevelRed,
			MonitorSetting.VideoBlackLevelGreen => GrpcMonitorSetting.VideoBlackLevelGreen,
			MonitorSetting.VideoBlackLevelBlue => GrpcMonitorSetting.VideoBlackLevelBlue,
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
			MonitorSetting.InputLag => GrpcMonitorSetting.InputLag,
			MonitorSetting.ResponseTime => GrpcMonitorSetting.ResponseTime,
			MonitorSetting.BlueLightFilterLevel => GrpcMonitorSetting.BlueLightFilterLevel,
			MonitorSetting.OsdLanguage => GrpcMonitorSetting.OsdLanguage,
			MonitorSetting.PowerIndicator => GrpcMonitorSetting.PowerIndicator,
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
			GrpcMonitorSetting.Sharpness => MonitorSetting.Sharpness,
			GrpcMonitorSetting.AudioVolume => MonitorSetting.AudioVolume,
			GrpcMonitorSetting.InputSelect => MonitorSetting.InputSelect,
			GrpcMonitorSetting.VideoGainRed => MonitorSetting.VideoGainRed,
			GrpcMonitorSetting.VideoGainGreen => MonitorSetting.VideoGainGreen,
			GrpcMonitorSetting.VideoGainBlue => MonitorSetting.VideoGainBlue,
			GrpcMonitorSetting.VideoBlackLevelRed => MonitorSetting.VideoBlackLevelRed,
			GrpcMonitorSetting.VideoBlackLevelGreen => MonitorSetting.VideoBlackLevelGreen,
			GrpcMonitorSetting.VideoBlackLevelBlue => MonitorSetting.VideoBlackLevelBlue,
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
			GrpcMonitorSetting.InputLag => MonitorSetting.InputLag,
			GrpcMonitorSetting.ResponseTime => MonitorSetting.ResponseTime,
			GrpcMonitorSetting.BlueLightFilterLevel => MonitorSetting.BlueLightFilterLevel,
			GrpcMonitorSetting.OsdLanguage => MonitorSetting.OsdLanguage,
			GrpcMonitorSetting.PowerIndicator => MonitorSetting.PowerIndicator,
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

	public static GrpcImageFormat ToGrpc(this ImageFormat imageFormat)
		=> imageFormat switch
		{
			ImageFormat.Raw => GrpcImageFormat.Raw,
			ImageFormat.Bitmap => GrpcImageFormat.Bitmap,
			ImageFormat.Gif => GrpcImageFormat.Gif,
			ImageFormat.Jpeg => GrpcImageFormat.Jpeg,
			ImageFormat.Png => GrpcImageFormat.Png,
			ImageFormat.WebP => GrpcImageFormat.WebP,
			_ => throw new NotImplementedException(),
		};
}
