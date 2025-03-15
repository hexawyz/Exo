namespace Exo.Discovery;

public readonly struct SystemCpuDeviceKey : IEquatable<SystemCpuDeviceKey>
{
	private readonly byte _index;

	private SystemCpuDeviceKey(byte index) => _index = index;

	public override bool Equals(object? obj) => obj is SystemCpuDeviceKey key && Equals(key);
	public bool Equals(SystemCpuDeviceKey other) => _index == other._index;
	public override int GetHashCode() => HashCode.Combine(_index);

	public static implicit operator SystemCpuDeviceKey(byte index) => new(index);
	public static explicit operator byte(SystemCpuDeviceKey key) => key._index;

	public static bool operator ==(SystemCpuDeviceKey left, SystemCpuDeviceKey right) => left.Equals(right);
	public static bool operator !=(SystemCpuDeviceKey left, SystemCpuDeviceKey right) => !(left == right);
}
