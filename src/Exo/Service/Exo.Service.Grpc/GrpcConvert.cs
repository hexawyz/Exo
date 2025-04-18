using System.Collections.Immutable;
using System.Numerics;
using Exo.Contracts.Ui.Settings.Cooling;
using Exo.Cooling.Configuration;
using Exo.Features.EmbeddedMonitors;
using Exo.Images;
using Exo.Monitors;
using CoolerType = Exo.Cooling.CoolerType;
using GrpcCoolerInformation = Exo.Contracts.Ui.Settings.CoolerInformation;
using GrpcCoolerPowerLimits = Exo.Contracts.Ui.Settings.CoolerPowerLimits;
using GrpcCoolerType = Exo.Contracts.Ui.Settings.CoolerType;
using GrpcCoolingControlCurve = Exo.Contracts.Ui.Settings.Cooling.CoolingControlCurve;
using GrpcCoolingDeviceInformation = Exo.Contracts.Ui.Settings.CoolingDeviceInformation;
using GrpcCoolingModes = Exo.Contracts.Ui.Settings.CoolingModes;
using GrpcCoolingParameters = Exo.Contracts.Ui.Settings.Cooling.CoolingParameters;
using GrpcEmbeddedMonitorConfigurationUpdate = Exo.Contracts.Ui.Settings.EmbeddedMonitorConfigurationUpdate;
using GrpcEmbeddedMonitorDeviceInformation = Exo.Contracts.Ui.Settings.EmbeddedMonitorDeviceInformation;
using GrpcEmbeddedMonitorGraphicsDescription = Exo.Contracts.Ui.Settings.EmbeddedMonitorGraphicsDescription;
using GrpcEmbeddedMonitorInformation = Exo.Contracts.Ui.Settings.EmbeddedMonitorInformation;
using GrpcLightCapabilities = Exo.Contracts.Ui.Settings.LightCapabilities;
using GrpcLightChangeNotification = Exo.Contracts.Ui.Settings.LightChangeNotification;
using GrpcLightDeviceCapabilities = Exo.Contracts.Ui.Settings.LightDeviceCapabilities;
using GrpcLightDeviceInformation = Exo.Contracts.Ui.Settings.LightDeviceInformation;
using GrpcLightInformation = Exo.Contracts.Ui.Settings.LightInformation;
using GrpcMonitorShape = Exo.Contracts.Ui.Settings.MonitorShape;
using GrpcRectangle = Exo.Contracts.Ui.Settings.Rectangle;
using GrpcSize = Exo.Contracts.Ui.Settings.Size;
using GrpcWatchNotificationKind = Exo.Contracts.Ui.WatchNotificationKind;

namespace Exo.Service.Grpc;

internal static class GrpcConvert
{
	public static GrpcEmbeddedMonitorDeviceInformation ToGrpc(this EmbeddedMonitorDeviceInformation embeddedMonitorDeviceInformation)
		=> new()
		{
			DeviceId = embeddedMonitorDeviceInformation.DeviceId,
			EmbeddedMonitors = ImmutableArray.CreateRange(embeddedMonitorDeviceInformation.EmbeddedMonitors, ToGrpc),
		};

	public static GrpcEmbeddedMonitorInformation ToGrpc(this EmbeddedMonitorInformation embeddedMonitorInformation)
		=> new()
		{
			MonitorId = embeddedMonitorInformation.MonitorId,
			Shape = embeddedMonitorInformation.Shape.ToGrpc(),
			ImageSize = embeddedMonitorInformation.ImageSize.ToGrpc(),
			Capabilities = (Contracts.Ui.Settings.EmbeddedMonitorCapabilities)embeddedMonitorInformation.Capabilities,
			SupportedImageFormats = (Contracts.Ui.Settings.ImageFormats)embeddedMonitorInformation.SupportedImageFormats,
			SupportedGraphics = ImmutableArray.CreateRange(embeddedMonitorInformation.SupportedGraphics, ToGrpc),
		};

	public static GrpcEmbeddedMonitorConfigurationUpdate ToGrpc(this EmbeddedMonitorConfigurationWatchNotification configurationUpdate)
		=> new()
		{
			DeviceId = configurationUpdate.DeviceId,
			MonitorId = configurationUpdate.MonitorId,
			GraphicsId = configurationUpdate.GraphicsId,
			ImageConfiguration = configurationUpdate.GraphicsId == default ?
				new() { ImageId = configurationUpdate.ImageId, ImageRegion = configurationUpdate.ImageRegion.ToGrpc() } :
				null,
		};

	public static GrpcEmbeddedMonitorGraphicsDescription ToGrpc(this EmbeddedMonitorGraphicsDescription description)
		=> new()
		{
			GraphicsId = description.GraphicsId,
			NameStringId = description.NameStringId,
		};

	public static GrpcMonitorShape ToGrpc(this MonitorShape shape)
		=> shape switch
		{
			MonitorShape.Rectangle => GrpcMonitorShape.Rectangle,
			MonitorShape.Square => GrpcMonitorShape.Square,
			MonitorShape.Circle => GrpcMonitorShape.Circle,
			_ => throw new NotImplementedException()
		};

	public static GrpcSize ToGrpc(this Size size) => new() { Width = size.Width, Height = size.Height };
	public static GrpcRectangle ToGrpc(this Rectangle rectangle) => new() { Left = rectangle.Left, Top = rectangle.Top, Width = rectangle.Width, Height = rectangle.Height };
	public static Rectangle FromGrpc(this GrpcRectangle rectangle) => new() { Left = rectangle.Left, Top = rectangle.Top, Width = rectangle.Width, Height = rectangle.Height };

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

	public static GrpcLightDeviceInformation ToGrpc(this LightDeviceInformation information)
		=> new()
		{
			DeviceId = information.DeviceId,
			Capabilities = (GrpcLightDeviceCapabilities)information.Capabilities,
			Lights = ImmutableArray.CreateRange(information.Lights, ToGrpc),
		};

	public static GrpcLightInformation ToGrpc(this LightInformation information)
		=> new()
		{
			LightId = information.LightId,
			Capabilities = (GrpcLightCapabilities)information.Capabilities,
			MinimumBrightness = information.MinimumBrightness,
			MaximumBrightness = information.MaximumBrightness,
			MinimumTemperature = information.MinimumTemperature,
			MaximumTemperature = information.MaximumTemperature,
		};

	public static GrpcLightChangeNotification ToGrpc(this LightChangeNotification notification)
		=> new()
		{
			DeviceId = notification.DeviceId,
			LightId = notification.LightId,
			IsOn = notification.IsOn,
			Brightness = notification.Brightness,
			Temperature = notification.Temperature,
		};
}
