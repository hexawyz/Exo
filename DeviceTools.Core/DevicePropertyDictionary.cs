using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DeviceTools;

public readonly struct DevicePropertyDictionary : IReadOnlyDictionary<PropertyKey, object?>, IDictionary<PropertyKey, object?>
{
	private Dictionary<PropertyKey, object?> Properties { get; }

	internal DevicePropertyDictionary(Dictionary<PropertyKey, object?> properties)
	{
		Properties = properties;
	}

	public object? this[PropertyKey key] => Properties[key];

	object? IDictionary<PropertyKey, object?>.this[PropertyKey key]
	{
		get => Properties[key];
		set => throw new NotSupportedException();
	}

	public int Count => Properties.Count;

	bool ICollection<KeyValuePair<PropertyKey, object?>>.IsReadOnly => true;

	public IEnumerable<PropertyKey> Keys => Properties.Keys;
	ICollection<PropertyKey> IDictionary<PropertyKey, object?>.Keys => Properties.Keys;

	public IEnumerable<object?> Values => Properties.Values;
	ICollection<object?> IDictionary<PropertyKey, object?>.Values => Properties.Values;

	public bool ContainsKey(PropertyKey key) => Properties.ContainsKey(key);

	public bool TryGetValue(PropertyKey key, out object? value) => Properties.TryGetValue(key, out value);

#if NETSTANDARD
	public bool TryGetValue<T>(PropertyKey key, out T? value)
#else
	public bool TryGetValue<T>(PropertyKey key, [NotNullWhen(true)] out T? value)
#endif
	{
		if (Properties.TryGetValue(key, out var obj) && obj is T v)
		{
			value = v;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}

	public IEnumerator<KeyValuePair<PropertyKey, object?>> GetEnumerator() => Properties.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	bool ICollection<KeyValuePair<PropertyKey, object?>>.Contains(KeyValuePair<PropertyKey, object?> item)
		=> ((ICollection<KeyValuePair<PropertyKey, object?>>)Properties).Contains(item);

	void ICollection<KeyValuePair<PropertyKey, object?>>.CopyTo(KeyValuePair<PropertyKey, object?>[] array, int arrayIndex)
		=> ((ICollection<KeyValuePair<PropertyKey, object?>>)Properties).CopyTo(array, arrayIndex);

	void IDictionary<PropertyKey, object?>.Add(PropertyKey key, object? value) => throw new NotSupportedException();
	void ICollection<KeyValuePair<PropertyKey, object?>>.Add(KeyValuePair<PropertyKey, object?> item) => throw new NotSupportedException();

	bool IDictionary<PropertyKey, object?>.Remove(PropertyKey key) => throw new NotSupportedException();
	bool ICollection<KeyValuePair<PropertyKey, object?>>.Remove(KeyValuePair<PropertyKey, object?> item) => throw new NotSupportedException();

	void ICollection<KeyValuePair<PropertyKey, object?>>.Clear() => throw new NotSupportedException();
}
