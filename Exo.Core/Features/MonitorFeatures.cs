using System.Collections.Immutable;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Images;

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

public interface IRawVcpFeature : IMonitorDeviceFeature
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

public interface IMonitorBrightnessFeature : IMonitorDeviceFeature
{
	ValueTask<ContinuousValue> GetBrightnessAsync(CancellationToken cancellationToken);
	ValueTask SetBrightnessAsync(ushort value, CancellationToken cancellationToken);
}

public interface IMonitorContrastFeature : IMonitorDeviceFeature
{
	ValueTask<ContinuousValue> GetContrastAsync(CancellationToken cancellationToken);
	ValueTask SetContrastAsync(ushort value, CancellationToken cancellationToken);
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

public interface IMonitorInputSelectFeature : IMonitorDeviceFeature
{
	ImmutableArray<NonContinuousValueDescription> InputSources { get; }
	ValueTask<ushort> GetInputSourceAsync(CancellationToken cancellationToken);
	ValueTask SetInputSourceAsync(ushort sourceId, CancellationToken cancellationToken);
}

// TODO: See how to implement this feature, as it is non-linear.
// MCCS VCP values should generally be bytes, with special values 00 for default (unknown ?) volume and FF for mute.
// Leaving it as-is is an option, but it is a bit weird to leave the interpretation of values up to the client.
public interface IMonitorSpeakerAudioVolumeFeature : IMonitorDeviceFeature
{
	ValueTask<ContinuousValue> GetVolumeAsync(CancellationToken cancellationToken);
	ValueTask SetVolumeAsync(ushort value, CancellationToken cancellationToken);
	//ValueTask SetDefaultVolumeAsync(CancellationToken cancellationToken);
	//ValueTask MuteAsync(CancellationToken cancellationToken);
}

public interface IEmbeddedMonitorInformationFeature : IMonitorDeviceFeature
{
	MonitorShape Shape { get; }
	Size ImageSize { get; }
}

public enum MonitorShape : byte
{
	Rectangle = 0,
	Square = 1,
	Circle = 2,
}
