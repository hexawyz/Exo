namespace Exo.Discovery;

public static class TypeExtensions
{
	internal static class TypeInfo<T>
	{
		public static readonly string? AssemblyFullName = typeof(T).Assembly.FullName;
		public static readonly string? TypeName = typeof(T).FullName;

		public static bool Matches(Type other)
			=> other.Assembly.FullName == AssemblyFullName && TypeName == other.FullName;
	}

	public static bool Matches<T>(this Type other)
		=> TypeInfo<T>.Matches(other);

	public static bool Matches(this Type type, Type other)
		=> type.Assembly.FullName == other.Assembly.FullName && type.FullName == other.FullName;

	public static bool MatchesGeneric(this Type type, Type other)
		=> type.IsGenericType && type.GetGenericTypeDefinition() is var genericType && genericType.Matches(other);
}
