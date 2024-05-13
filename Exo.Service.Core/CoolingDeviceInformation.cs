using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct CoolingDeviceInformation : IEquatable<CoolingDeviceInformation>
{
	public CoolingDeviceInformation(Guid deviceId, ImmutableArray<CoolerInformation> coolers)
	{
		DeviceId = deviceId;
		Coolers = coolers;
	}

	public Guid DeviceId { get; }
	public ImmutableArray<CoolerInformation> Coolers { get; }

	public override bool Equals(object? obj) => obj is CoolingDeviceInformation information && Equals(information);
	public bool Equals(CoolingDeviceInformation other) => DeviceId.Equals(other.DeviceId) && Coolers.SequenceEqual(other.Coolers);
	public override int GetHashCode() => HashCode.Combine(DeviceId, Coolers.Length);

	public static bool operator ==(CoolingDeviceInformation left, CoolingDeviceInformation right) => left.Equals(right);
	public static bool operator !=(CoolingDeviceInformation left, CoolingDeviceInformation right) => !(left == right);
}
