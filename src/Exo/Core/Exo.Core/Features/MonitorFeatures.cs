using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Images;
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

public readonly record struct ContinuousValue : IEquatable<ContinuousValue>
{
	public ContinuousValue(ushort current, ushort minimum, ushort maximum)
	{
		Current = current;
		Minimum = minimum;
		Maximum = maximum;
	}

	public ushort Current { get; init; }
	public ushort Minimum { get; init; }
	public ushort Maximum { get; init; }
}

/// <summary>Provides information about a non continuous value, such as an input source.</summary>
/// <remarks>
/// While there is a standard for some non-continuous settings, it is not always exhaustive and various implementations can decide to extend it.
/// As such, we provide explicit information about non-continuous values, with the best of our knowledge.
/// </remarks>
public readonly struct NonContinuousValueDescription
{
	public NonContinuousValueDescription(ushort value, Guid nameIdString, string? customName)
	{
		Value = value;
		NameStringId = nameIdString;
		CustomName = customName;
	}

	/// <summary>Gets the value associated with this input source.</summary>
	public ushort Value { get; }
	/// <summary>Gets the name string ID for this value.</summary>
	/// <remarks>
	/// Non-continuous values that have been mapped by an explicit monitor description should generally provide this value.
	/// If possible, the name will be provided based on the values defined in the standard.
	/// If this ID is not provided, the value of the property will be <see cref="Guid.Empty"/>.
	/// </remarks>
	public Guid NameStringId { get; }
	/// <summary>Gets the custom name of the value.</summary>
	/// <remarks>
	/// Although rarely used, if used at all, monitors can provide custom name for non continuous values as part of their capabilities string.
	/// If this were to be the case, this property will contain the value contained in the capabilities string.
	/// </remarks>
	public string? CustomName { get; }
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
