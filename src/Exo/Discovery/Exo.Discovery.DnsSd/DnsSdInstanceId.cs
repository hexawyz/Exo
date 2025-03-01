namespace Exo.Discovery;

public readonly struct DnsSdInstanceId(string instanceId) : IEquatable<DnsSdInstanceId>
{
	private readonly string _instanceId = instanceId;
	public override string ToString() => _instanceId;

	public override bool Equals(object? obj) => obj is DnsSdInstanceId path && Equals(path);
	public bool Equals(DnsSdInstanceId other) => _instanceId == other._instanceId;
	public override int GetHashCode() => HashCode.Combine(_instanceId);

	public static bool operator ==(DnsSdInstanceId left, DnsSdInstanceId right) => left.Equals(right);
	public static bool operator !=(DnsSdInstanceId left, DnsSdInstanceId right) => !(left == right);

	public static implicit operator DnsSdInstanceId(string path) => new(path);
	public static explicit operator string(DnsSdInstanceId path) => path._instanceId;
}
