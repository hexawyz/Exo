using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct SensorDeviceInformation : IEquatable<SensorDeviceInformation>
{
	public SensorDeviceInformation(Guid deviceId, bool isConnected, ImmutableArray<SensorInformation> sensors)
	{
		DeviceId = deviceId;
		IsConnected = isConnected;
		Sensors = sensors;
	}

	public Guid DeviceId { get; }
	public bool IsConnected { get; }
	public ImmutableArray<SensorInformation> Sensors { get; }

	public override bool Equals(object? obj) => obj is SensorDeviceInformation information && Equals(information);
	public bool Equals(SensorDeviceInformation other) => DeviceId.Equals(other.DeviceId) && IsConnected == other.IsConnected && Sensors.SequenceEqual(other.Sensors);
	public override int GetHashCode() => HashCode.Combine(DeviceId, IsConnected, Sensors.Length);

	public static bool operator ==(SensorDeviceInformation left, SensorDeviceInformation right) => left.Equals(right);
	public static bool operator !=(SensorDeviceInformation left, SensorDeviceInformation right) => !(left == right);
}
