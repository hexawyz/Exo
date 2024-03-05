using System.Reflection;
using System.Runtime.CompilerServices;

namespace Exo;

public static class TypeId
{
	private delegate ref readonly Guid? GetValueRefDelegate();

	private static readonly ConditionalWeakTable<Type, GetValueRefDelegate> TypeIdRefAccessors = new();

	private static readonly MethodInfo GetValueRefMethod =
		typeof(TypeId)
			.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
			.Single(m => m.Name == nameof(GetValueRef) && m.ContainsGenericParameters);

	private static Guid? GetNonCached(Type featureType) => featureType.GetCustomAttribute<TypeIdAttribute>()?.Value;

	private static ref readonly Guid? GetValueRef(Type type)
		=> ref TypeIdRefAccessors.GetValue(type, t => GetValueRefMethod.MakeGenericMethod(type).CreateDelegate<GetValueRefDelegate>(null))();

	public static Guid Get(Type type) => GetValueRef(type) ?? throw new InvalidOperationException($"The type {type} does not have a unique identifier.");

	public static Guid Get<T>() => Cache<T>.Value ?? throw new InvalidOperationException($"The type {typeof(T)} does not have a unique identifier.");
	public static string GetString<T>() => Cache<T>.StringValue ?? throw new InvalidOperationException($"The type {typeof(T)} does not have a unique identifier.");

	public static bool TryGet(Type type, out Guid id)
	{
		ref readonly var value = ref GetValueRef(type);
		id = value.GetValueOrDefault();
		return value.HasValue;
	}

	public static bool TryGet<T>(out Guid id)
	{
		ref readonly var value = ref Cache<T>.Value;
		id = value.GetValueOrDefault();
		return value.HasValue;
	}

	private static ref readonly Guid? GetValueRef<T>(object? _) => ref Cache<T>.Value;

	private static class Cache<T>
	{
		public static readonly Guid? Value = GetNonCached(typeof(T));
		public static readonly string? StringValue = Value?.ToString("D");
	}
}
