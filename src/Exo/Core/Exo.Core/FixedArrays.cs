using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Exo;

#pragma warning disable IDE0044
[InlineArray(2)]
public struct FixedArray2<T>
	where T : unmanaged
{
	private T _element0;
}

[InlineArray(5)]
public struct FixedArray5<T>
	where T : unmanaged
{
	private T _element0;
}

[InlineArray(8)]
public struct FixedArray8<T>
	where T : unmanaged
{
	private T _element0;
}

[InlineArray(10)]
public struct FixedArray10<T>
	where T : unmanaged
{
	private T _element0;
}

[InlineArray(12)]
public struct FixedArray12<T>
	where T : unmanaged
{
	private T _element0;
}

[InlineArray(16)]
public struct FixedArray16<T>
	where T : unmanaged
{
	private T _element0;
}

[InlineArray(18)]
public struct FixedArray18<T>
	where T : unmanaged
{
	private T _element0;
}

[InlineArray(32)]
public struct FixedArray32<T>
	where T : unmanaged
{
	private T _element0;
}
#pragma warning restore IDE0044 // Add readonly modifier

// This implementation can easily be copied for any size by changing the size in the first few lines.
public struct FixedList2<T> : IList<T>
	where T : unmanaged, IEquatable<T>
{
	private const int Capacity = 2;

	public static implicit operator ReadOnlySpan<T>(in FixedList2<T> value) => ((ReadOnlySpan<T>)value._data)[..value._count];

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static void UnsafePrepare(uint count, out FixedList2<T> list)
	{
		if (count > Capacity) throw new ArgumentOutOfRangeException(nameof(count));
		Unsafe.SkipInit(out list);
		list._count = 0;
		((Span<T>)list._data)[(int)count..].Clear();
	}

	private byte _count;
	private FixedArray2<T> _data;

	public FixedList2(ReadOnlySpan<T> values)
	{
		if (values.Length > Capacity) throw new ArgumentException(null, nameof(values));
		_count = (byte)values.Length;
		values.CopyTo(_data);
	}

	public readonly byte Count => _count;
	readonly int ICollection<T>.Count => _count;

	public T this[int index]
	{
		readonly get
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			return ((ReadOnlySpan<T>)_data)[index];
		}
		set
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			((Span<T>)_data)[index] = value;
		}
	}

	readonly bool ICollection<T>.IsReadOnly => false;

	public void Add(T value)
	{
		if (_count >= Capacity) throw new InvalidOperationException();
		((Span<T>)_data)[_count] = value;
		++_count;
	}

	public void Insert(int index, T item)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _count);
		if (_count >= Capacity) throw new InvalidOperationException();
		Span<T> data = _data;
		if (index < _count) data[index..].CopyTo(data[index..^1]);
		data[index] = item;
		++_count;
	}

	public bool Remove(T item)
	{
		var data = (Span<T>)_data;
		int index = data[0.._count].IndexOf(item);
		if (index < 0) return false;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
		return true;
	}

	public void RemoveAt(int index)
	{
		if ((uint)index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
		Span<T> data = _data;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
	}

	public void Clear()
	{
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) ((Span<T>)_data)[0.._count].Clear();
		_count = 0;
	}

	public readonly bool Contains(T item) => IndexOf(item) >= 0;
	public readonly int IndexOf(T item) => ((ReadOnlySpan<T>)_data)[0.._count].IndexOf(item);

	public readonly void CopyTo(T[] array, int arrayIndex) => ((ReadOnlySpan<T>)_data)[0.._count].CopyTo(array.AsSpan(arrayIndex));

	readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();
	readonly IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
}

public struct FixedList5<T> : IList<T>
	where T : unmanaged, IEquatable<T>
{
	private const int Capacity = 5;

	public static implicit operator ReadOnlySpan<T>(in FixedList5<T> value) => ((ReadOnlySpan<T>)value._data)[..value._count];

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static void UnsafePrepare(uint count, out FixedList5<T> list)
	{
		if (count > Capacity) throw new ArgumentOutOfRangeException(nameof(count));
		Unsafe.SkipInit(out list);
		list._count = 0;
		((Span<T>)list._data)[(int)count..].Clear();
	}

	private byte _count;
	private FixedArray5<T> _data;

	public FixedList5(ReadOnlySpan<T> values)
	{
		if (values.Length > Capacity) throw new ArgumentException(null, nameof(values));
		_count = (byte)values.Length;
		values.CopyTo(_data);
	}

	public readonly byte Count => _count;
	readonly int ICollection<T>.Count => _count;

	public T this[int index]
	{
		readonly get
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			return ((ReadOnlySpan<T>)_data)[index];
		}
		set
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			((Span<T>)_data)[index] = value;
		}
	}

	readonly bool ICollection<T>.IsReadOnly => false;

	public void Add(T value)
	{
		if (_count >= Capacity) throw new InvalidOperationException();
		((Span<T>)_data)[_count] = value;
		++_count;
	}

	public void Insert(int index, T item)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _count);
		if (_count >= Capacity) throw new InvalidOperationException();
		Span<T> data = _data;
		if (index < _count) data[index..].CopyTo(data[index..^1]);
		data[index] = item;
		++_count;
	}

	public bool Remove(T item)
	{
		var data = (Span<T>)_data;
		int index = data[0.._count].IndexOf(item);
		if (index < 0) return false;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
		return true;
	}

	public void RemoveAt(int index)
	{
		if ((uint)index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
		Span<T> data = _data;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
	}

	public void Clear()
	{
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) ((Span<T>)_data)[0.._count].Clear();
		_count = 0;
	}

	public readonly bool Contains(T item) => IndexOf(item) >= 0;
	public readonly int IndexOf(T item) => ((ReadOnlySpan<T>)_data)[0.._count].IndexOf(item);

	public readonly void CopyTo(T[] array, int arrayIndex) => ((ReadOnlySpan<T>)_data)[0.._count].CopyTo(array.AsSpan(arrayIndex));

	readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();
	readonly IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
}

public struct FixedList8<T> : IList<T>
	where T : unmanaged, IEquatable<T>
{
	private const int Capacity = 8;

	public static implicit operator ReadOnlySpan<T>(in FixedList8<T> value) => ((ReadOnlySpan<T>)value._data)[..value._count];

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static void UnsafePrepare(uint count, out FixedList8<T> list)
	{
		if (count > Capacity) throw new ArgumentOutOfRangeException(nameof(count));
		Unsafe.SkipInit(out list);
		list._count = 0;
		((Span<T>)list._data)[(int)count..].Clear();
	}

	private byte _count;
	private FixedArray8<T> _data;

	public FixedList8(ReadOnlySpan<T> values)
	{
		if (values.Length > Capacity) throw new ArgumentException(null, nameof(values));
		_count = (byte)values.Length;
		values.CopyTo(_data);
	}

	public readonly byte Count => _count;
	readonly int ICollection<T>.Count => _count;

	public T this[int index]
	{
		readonly get
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			return ((ReadOnlySpan<T>)_data)[index];
		}
		set
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			((Span<T>)_data)[index] = value;
		}
	}

	readonly bool ICollection<T>.IsReadOnly => false;

	public void Add(T value)
	{
		if (_count >= Capacity) throw new InvalidOperationException();
		((Span<T>)_data)[_count] = value;
		++_count;
	}

	public void Insert(int index, T item)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _count);
		if (_count >= Capacity) throw new InvalidOperationException();
		Span<T> data = _data;
		if (index < _count) data[index..].CopyTo(data[index..^1]);
		data[index] = item;
		++_count;
	}

	public bool Remove(T item)
	{
		var data = (Span<T>)_data;
		int index = data[0.._count].IndexOf(item);
		if (index < 0) return false;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
		return true;
	}

	public void RemoveAt(int index)
	{
		if ((uint)index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
		Span<T> data = _data;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
	}

	public void Clear()
	{
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) ((Span<T>)_data)[0.._count].Clear();
		_count = 0;
	}

	public readonly bool Contains(T item) => IndexOf(item) >= 0;
	public readonly int IndexOf(T item) => ((ReadOnlySpan<T>)_data)[0.._count].IndexOf(item);

	public readonly void CopyTo(T[] array) => ((ReadOnlySpan<T>)_data)[0.._count].CopyTo(array.AsSpan());
	public readonly void CopyTo(T[] array, int arrayIndex) => ((ReadOnlySpan<T>)_data)[0.._count].CopyTo(array.AsSpan(arrayIndex));

	readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();
	readonly IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
}

public struct FixedList10<T> : IList<T>
	where T : unmanaged, IEquatable<T>
{
	private const int Capacity = 10;

	public static implicit operator ReadOnlySpan<T>(in FixedList10<T> value) => ((ReadOnlySpan<T>)value._data)[..value._count];

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static void UnsafePrepare(uint count, out FixedList10<T> list)
	{
		if (count > Capacity) throw new ArgumentOutOfRangeException(nameof(count));
		Unsafe.SkipInit(out list);
		list._count = 0;
		((Span<T>)list._data)[(int)count..].Clear();
	}

	private byte _count;
	private FixedArray10<T> _data;

	public FixedList10(ReadOnlySpan<T> values)
	{
		if (values.Length > Capacity) throw new ArgumentException(null, nameof(values));
		_count = (byte)values.Length;
		values.CopyTo(_data);
	}

	public readonly byte Count => _count;
	readonly int ICollection<T>.Count => _count;

	public T this[int index]
	{
		readonly get
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			return ((ReadOnlySpan<T>)_data)[index];
		}
		set
		{
			if ((uint)index >= _count) throw new IndexOutOfRangeException();
			((Span<T>)_data)[index] = value;
		}
	}

	readonly bool ICollection<T>.IsReadOnly => false;

	public void Add(T value)
	{
		if (_count >= Capacity) throw new InvalidOperationException();
		((Span<T>)_data)[_count] = value;
		++_count;
	}

	public void Insert(int index, T item)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _count);
		if (_count >= Capacity) throw new InvalidOperationException();
		Span<T> data = _data;
		if (index < _count) data[index..].CopyTo(data[index..^1]);
		data[index] = item;
		++_count;
	}

	public bool Remove(T item)
	{
		var data = (Span<T>)_data;
		int index = data[0.._count].IndexOf(item);
		if (index < 0) return false;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
		return true;
	}

	public void RemoveAt(int index)
	{
		if ((uint)index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
		Span<T> data = _data;
		data[(index + 1)..].CopyTo(data[index..]);
		--_count;
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) _data[_count] = default;
	}

	public void Clear()
	{
		//if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) ((Span<T>)_data)[0.._count].Clear();
		_count = 0;
	}

	public readonly bool Contains(T item) => IndexOf(item) >= 0;
	public readonly int IndexOf(T item) => ((ReadOnlySpan<T>)_data)[0.._count].IndexOf(item);

	public readonly void CopyTo(T[] array) => ((ReadOnlySpan<T>)_data)[0.._count].CopyTo(array.AsSpan());
	public readonly void CopyTo(T[] array, int arrayIndex) => ((ReadOnlySpan<T>)_data)[0.._count].CopyTo(array.AsSpan(arrayIndex));

	readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();
	readonly IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
}
