using System.Collections.Immutable;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Monitors;

namespace Exo.Features.Monitors;

/// <summary>This feature allows to retrieve the raw capabilities of the monitor, as cached by the driver.</summary>
public interface IMonitorRawCapabilitiesFeature : IMonitorDeviceFeature
{
	ReadOnlySpan<byte> RawCapabilities { get; }
}

/// <summary>This feature allows to retrieve the capabilities of the monitor.</summary>
/// <remarks>These capabilities do not necessarily map to the raw capabilities value returned by the monitor.</remarks>
public interface IMonitorCapabilitiesFeature : IMonitorDeviceFeature
{
	MonitorCapabilities Capabilities { get; }
}

public interface IMonitorRawVcpFeature : IMonitorDeviceFeature
{
	ValueTask SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken);
	ValueTask<VcpFeatureReply> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken);
}

public interface IContinuousVcpFeature
{
	ValueTask<ContinuousValue> GetValueAsync(CancellationToken cancellationToken);
	ValueTask SetValueAsync(ushort value, CancellationToken cancellationToken);
}

public interface INonContinuousVcpFeature
{
	ImmutableArray<NonContinuousValueDescription> AllowedValues { get; }
	ValueTask<ushort> GetValueAsync(CancellationToken cancellationToken);
	ValueTask SetValueAsync(ushort value, CancellationToken cancellationToken);
}

public interface IBooleanVcpFeature
{
	ValueTask<bool> GetValueAsync(CancellationToken cancellationToken);
	ValueTask SetValueAsync(bool value, CancellationToken cancellationToken);
}

public interface IMonitorBrightnessFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorContrastFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorSharpnessFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedVideoGainFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenVideoGainFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueVideoGainFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedVideoBlackLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenVideoBlackLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueVideoBlackLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorYellowSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorCyanSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorMagentaSixAxisSaturationControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorRedSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorYellowSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorGreenSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorCyanSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorBlueSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorMagentaSixAxisHueControlFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }

public interface IMonitorInputSelectFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorSpeakerAudioVolumeFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }

public interface IMonitorOsdLanguageFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorResponseTimeFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorInputLagFeature : IMonitorDeviceFeature, INonContinuousVcpFeature { }
public interface IMonitorBlueLightFilterLevelFeature : IMonitorDeviceFeature, IContinuousVcpFeature { }
public interface IMonitorPowerIndicatorToggleFeature : IMonitorDeviceFeature, IBooleanVcpFeature { }
