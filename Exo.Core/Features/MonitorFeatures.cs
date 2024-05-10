using System.Collections.Immutable;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using Exo.Images;

namespace Exo.Features.MonitorFeatures;

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

public readonly struct InputSourceDescription
{
	public byte Value { get; }
	public string Name { get; }
}

public interface IMonitorInputSelectFeature : IMonitorDeviceFeature
{
	ImmutableArray<InputSourceDescription> InputSources { get; }
	byte GetCurrentSourceId();
	void SurCurrentSourceId(byte sourceId);
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
