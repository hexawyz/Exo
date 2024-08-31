namespace Exo.Discovery;

public readonly struct SystemDevicePath(string path) : IEquatable<SystemDevicePath>
{
	private readonly string _path = path;
	public override string ToString() => _path;

	public override bool Equals(object? obj) => obj is SystemDevicePath path && Equals(path);
	public bool Equals(SystemDevicePath other) => _path == other._path;
	public override int GetHashCode() => HashCode.Combine(_path);

	public static bool operator ==(SystemDevicePath left, SystemDevicePath right) => left.Equals(right);
	public static bool operator !=(SystemDevicePath left, SystemDevicePath right) => !(left == right);

	public static implicit operator SystemDevicePath(string path) => new(path);
	public static explicit operator string(SystemDevicePath path) => path._path;
}
