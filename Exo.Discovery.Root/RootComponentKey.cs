using System.Diagnostics.CodeAnalysis;

namespace Exo.Discovery;

public readonly struct RootComponentKey(Type type) : IEquatable<RootComponentKey>
{
	private readonly string _assemblyQualifiedName = type.AssemblyQualifiedName ?? throw new InvalidOperationException();

	public override string ToString() => _assemblyQualifiedName.ToString();

	public bool Equals(RootComponentKey other) => _assemblyQualifiedName == other._assemblyQualifiedName;
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is RootComponentKey key && Equals(key);

	public override int GetHashCode() => _assemblyQualifiedName?.GetHashCode() ?? 0;

	public static implicit operator RootComponentKey(Type type) => new(type);
	public static explicit operator string(RootComponentKey key) => key._assemblyQualifiedName;

	public static bool operator ==(RootComponentKey left, RootComponentKey right) => left.Equals(right);
	public static bool operator !=(RootComponentKey left, RootComponentKey right) => !(left == right);
}
