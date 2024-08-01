using System.Collections.Immutable;

namespace Exo.Contracts;

public static class ImmutableArrayExtensions
{
	public static ImmutableArray<T> NotNull<T>(this ImmutableArray<T> array) => array.IsDefault ? [] : array;
}
