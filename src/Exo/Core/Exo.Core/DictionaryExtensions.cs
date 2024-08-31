using System.Collections.Immutable;

namespace Exo;

public static class DictionaryExtensions
{
	public static void Add<TKey, TValue>(this Dictionary<TKey, List<TValue>> dictionary, TKey key, TValue value)
		where TKey : notnull
	{
		if (!dictionary.TryGetValue(key, out var list))
		{
			dictionary.Add(key, list = []);
		}
		list.Add(value);
	}

	public static void Remove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
		where TKey : notnull
		=> ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new(key, value));

	public static void Remove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, HashSet<TKey> keys, TValue value)
		where TKey : notnull
	{
		foreach (var key in keys)
		{
			dictionary.Remove(key, value);
		}
	}

	public static void Remove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, ImmutableArray<TKey> keys, TValue value)
		where TKey : notnull
	{
		foreach (var key in keys)
		{
			dictionary.Remove(key, value);
		}
	}
}
