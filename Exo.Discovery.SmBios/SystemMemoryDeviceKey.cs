namespace Exo.Discovery;

public readonly struct SystemMemoryDeviceKey : IEquatable<SystemMemoryDeviceKey>
{
	private readonly byte _index;

	private SystemMemoryDeviceKey(byte index) => _index = index;

	public override bool Equals(object? obj) => obj is SystemMemoryDeviceKey key && Equals(key);
	public bool Equals(SystemMemoryDeviceKey other) => _index == other._index;
	public override int GetHashCode() => HashCode.Combine(_index);

	public static implicit operator SystemMemoryDeviceKey(byte index) => new(index);
	public static explicit operator byte(SystemMemoryDeviceKey key) => key._index;

	public static bool operator ==(SystemMemoryDeviceKey left, SystemMemoryDeviceKey right) => left.Equals(right);
	public static bool operator !=(SystemMemoryDeviceKey left, SystemMemoryDeviceKey right) => !(left == right);
}
