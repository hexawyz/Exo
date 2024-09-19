using System.Diagnostics.CodeAnalysis;
using DeviceTools.FilterExpressions;

namespace DeviceTools;

public abstract class Property
{
#if !NETSTANDARD2_0
	public static bool TryGetByName(string name, [NotNullWhen(true)] out Property? property) =>
#else
	public static bool TryGetByName(string name, out Property? property) =>
#endif
		Properties.TryGetByName(name, out property!);

#if !NETSTANDARD2_0
	public static bool TryGetByKey(PropertyKey key, [NotNullWhen(true)] out Property? property) =>
#else
	public static bool TryGetByKey(PropertyKey key, out Property? property) =>
#endif
		Properties.TryGetByKey(key, out property);

	private readonly PropertyKey _key;
	public ref readonly PropertyKey Key => ref _key;
	public abstract DevicePropertyType Type { get; }

	private protected Property(Guid categoryId, uint propertyId) => _key = new(categoryId, propertyId);

	// All predefined properties should have a name, but if we allow custom properties to be instanciated, it won't be the case anymore.
	// That's why the name is not part of the property data. We may still decide to change that in the future, though.
#if !NETSTANDARD2_0
	public bool TryGetName(out string? name) =>
#else
	public bool TryGetName(out string? name) =>
#endif
		Properties.TryGetName(_key, out name);

#if !NETSTANDARD2_0
	public override int GetHashCode() => HashCode.Combine(Type, _key);
#else
		public override int GetHashCode()
		{
			int hashCode = -553669671;
			hashCode = hashCode * -1521134295 + Type.GetHashCode();
			hashCode = hashCode * -1521134295 + _key.GetHashCode();
			return hashCode;
		}
#endif

	public override bool Equals(object? obj) => obj is Property p && Equals(p);

	private bool Equals(Property other) => Type == other.Type && _key == other._key;
}

public abstract class Property<TValue> : Property
{
	private protected Property(Guid categoryId, uint propertyId) : base(categoryId, propertyId) { }

	public DeviceFilterExpression Exists() => new DeviceFilterExistenceExpression(this, true);
}
