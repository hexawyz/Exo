namespace Exo.Debug;

public readonly struct DebugDeviceKey : IEquatable<DebugDeviceKey>
{
	private readonly Guid _deviceId;

	private DebugDeviceKey(Guid deviceId) => _deviceId = deviceId;

	public override bool Equals(object? obj) => obj is DebugDeviceKey key && Equals(key);
	public bool Equals(DebugDeviceKey other) => _deviceId.Equals(other._deviceId);
	public override int GetHashCode() => HashCode.Combine(_deviceId);

	public static bool operator ==(DebugDeviceKey left, DebugDeviceKey right) => left.Equals(right);
	public static bool operator !=(DebugDeviceKey left, DebugDeviceKey right) => !(left == right);

	public static implicit operator DebugDeviceKey(Guid deviceId) => new(deviceId);
	public static explicit operator Guid(DebugDeviceKey key) => key._deviceId;
}
