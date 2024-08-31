using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct SensorDeviceInformation : IEquatable<SensorDeviceInformation>
{
	public SensorDeviceInformation(Guid deviceId, ImmutableArray<SensorInformation> sensors)
	{
		DeviceId = deviceId;
		Sensors = sensors;
	}

	public Guid DeviceId { get; }
	public ImmutableArray<SensorInformation> Sensors { get; }

	public override bool Equals(object? obj) => obj is SensorDeviceInformation information && Equals(information);
	public bool Equals(SensorDeviceInformation other) => DeviceId.Equals(other.DeviceId) && Sensors.SequenceEqual(other.Sensors);
	public override int GetHashCode() => HashCode.Combine(DeviceId, Sensors.Length);

	public static bool operator ==(SensorDeviceInformation left, SensorDeviceInformation right) => left.Equals(right);
	public static bool operator !=(SensorDeviceInformation left, SensorDeviceInformation right) => !(left == right);
}
