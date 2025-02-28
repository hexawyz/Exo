namespace Exo.Discovery;

public readonly struct DnsSdInstanceId(string name) : IEquatable<DnsSdInstanceId>
{
	private readonly string _path = name;
	public override string ToString() => _path;

	public override bool Equals(object? obj) => obj is DnsSdInstanceId path && Equals(path);
	public bool Equals(DnsSdInstanceId other) => _path == other._path;
	public override int GetHashCode() => HashCode.Combine(_path);

	public static bool operator ==(DnsSdInstanceId left, DnsSdInstanceId right) => left.Equals(right);
	public static bool operator !=(DnsSdInstanceId left, DnsSdInstanceId right) => !(left == right);

	public static implicit operator DnsSdInstanceId(string path) => new(path);
	public static explicit operator string(DnsSdInstanceId path) => path._path;
}
