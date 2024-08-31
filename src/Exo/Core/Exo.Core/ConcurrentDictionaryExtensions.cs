using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Exo;

public static class ConcurrentDictionaryExtensions
{
	public static void Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, HashSet<TKey> keys, TValue value)
		where TKey : notnull
	{
		foreach (var key in keys)
		{
			dictionary.TryRemove(new(key, value));
		}
	}

	public static void Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, ImmutableArray<TKey> keys, TValue value)
		where TKey : notnull
	{
		foreach (var key in keys)
		{
			dictionary.TryRemove(new(key, value));
		}
	}
}
