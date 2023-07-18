using System.Collections.Immutable;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;

namespace Exo.Features.MonitorFeatures;

public interface IMonitorDeviceFeature : IDeviceFeature
{
}

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

public readonly struct ContinuousValue : IEquatable<ContinuousValue>
{
	public ContinuousValue(ushort minimum, ushort current, ushort maximum)
	{
		Minimum = minimum;
		Current = current;
		Maximum = maximum;
	}

	public ushort Minimum { get; }
	public ushort Current { get; }
	public ushort Maximum { get; }

	public override bool Equals(object? obj) => obj is ContinuousValue value && Equals(value);
	public bool Equals(ContinuousValue other) => Minimum == other.Minimum && Current == other.Current && Maximum == other.Maximum;
	public override int GetHashCode() => HashCode.Combine(Minimum, Current, Maximum);

	public static bool operator ==(ContinuousValue left, ContinuousValue right) => left.Equals(right);
	public static bool operator !=(ContinuousValue left, ContinuousValue right) => !(left == right);
}

public interface IBrightnessFeature : IMonitorDeviceFeature
{
	ValueTask<ContinuousValue> GetBrightnessAsync(CancellationToken cancellationToken);
	ValueTask SetBrightnessAsync(ushort value, CancellationToken cancellationToken);
}

public interface IContrastFeature : IMonitorDeviceFeature
{
	ValueTask<ContinuousValue> GetContrastAsync(CancellationToken cancellationToken);
	ValueTask SetContrastAsync(ushort value, CancellationToken cancellationToken);
}

public readonly struct InputSourceDescription
{
	public byte Value { get; }
	public string Name { get; }
}

public interface IInputSelectFeature : IMonitorDeviceFeature
{
	ImmutableArray<InputSourceDescription> InputSources { get; }
	byte GetCurrentSourceId();
	void SurCurrentSourceId(byte sourceId);
}
