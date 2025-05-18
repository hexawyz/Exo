using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Exo;

public static class ArrayExtensions
{
	public static int Length<T>(this ImmutableArray<T> array)
		=> !array.IsDefault ? array.Length : 0;

	public static int GetSequenceHashCode<T>(this ImmutableArray<T> array)
		=> GetSequenceHashCode(ImmutableCollectionsMarshal.AsArray(array)!);

	public static int GetSequenceHashCode<T>(this T[] array)
	{
		if (array is null) return 0;

		return array.Length switch
		{
			0 => 0,
			1 => array[0]?.GetHashCode() ?? 0,
			2 => HashCode.Combine(array[0], array[1]),
			3 => HashCode.Combine(array[0], array[1], array[2]),
			4 => HashCode.Combine(array[0], array[1], array[2], array[3]),
			5 => HashCode.Combine(array[0], array[1], array[2], array[3], array[4]),
			6 => HashCode.Combine(array[0], array[1], array[2], array[3], array[4], array[5]),
			7 => HashCode.Combine(array[0], array[1], array[2], array[3], array[4], array[5], array[6]),
			8 => HashCode.Combine(array[0], array[1], array[2], array[3], array[4], array[5], array[6], array[7]),
			_ => LongArrayGetHashCode(array),
		};
	}

	private static int LongArrayGetHashCode<T>(this T[] array)
	{
		var hash = new HashCode();
		for (int i = 0; i < array.Length; i++)
		{
			hash.Add(array[i]);
		}
		return hash.ToHashCode();
	}

	internal static T[] Add<T>(this T[]? array, T item)
	{
		if (array is null) return [item];

		Array.Resize(ref array, array.Length + 1);
		array[^1] = item;

		return array;
	}

	internal static T[]? Remove<T>(this T[]? array, T item)
	{
		if (array is null) return null;

		if (Array.IndexOf(array, item) is int index and >= 0)
		{
			int newLength = array.Length - 1;

			if (newLength == 0) return null;

			var newArray = new T[newLength]; 

			Array.Copy(array, 0, newArray, 0, index);
			Array.Copy(array, index + 1, newArray, index, newLength - index);

			array = newArray;
		}

		return array;
	}

	public static int InterlockedAdd<T>(ref T[]? array, T item)
	{
		var a = array;
		while (true)
		{
			var b = a.Add(item);
			if (a == (a = Interlocked.CompareExchange(ref array, b, a)))
			{
				return b.Length;
			}
		}
	}

	public static int InterlockedRemove<T>(ref T[]? array, T item)
	{
		var a = array;
		while (true)
		{
			var b = a.Remove(item);
			if (a == (a = Interlocked.CompareExchange(ref a, b, a)))
			{
				return b?.Length ?? 0;
			}
		}
	}

	public static void Invoke<T>(this Action<T>[]? actions, T obj)
	{
		if (actions is null) return;

		int i = 0;
		try
		{
			for (; i < actions.Length; i++)
			{
				actions[i](obj);
			}
		}
		catch (Exception ex)
		{
			InvokeSlow(actions, obj, i, ex);
		}
	}

	private static void InvokeSlow<T>(this Action<T>[] actions, T obj, int i, Exception exception)
	{
		var exceptions = new List<Exception> { exception };
		for (; i < actions.Length; i++)
		{
			try
			{
				actions[i](obj);
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}
		throw new AggregateException(exceptions.ToArray());
	}

	public static void Invoke<T>(this RefReadonlyAction<T>[]? actions, in T obj)
	{
		if (actions is null) return;

		int i = 0;
		try
		{
			for (; i < actions.Length; i++)
			{
				actions[i](obj);
			}
		}
		catch (Exception ex)
		{
			InvokeSlow(actions, obj, i, ex);
		}
	}

	private static void InvokeSlow<T>(this RefReadonlyAction<T>[] actions, in T obj, int i, Exception exception)
	{
		var exceptions = new List<Exception> { exception };
		for (; i < actions.Length; i++)
		{
			try
			{
				actions[i](obj);
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}
		throw new AggregateException(exceptions.ToArray());
	}

	public static void Invoke<T1, T2>(this Action<T1, T2>[]? actions, T1 arg1, T2 arg2)
	{
		if (actions is null) return;

		int i = 0;
		try
		{
			for (; i < actions.Length; i++)
			{
				actions[i](arg1, arg2);
			}
		}
		catch (Exception ex)
		{
			InvokeSlow(actions, arg1, arg2, i, ex);
		}
	}

	private static void InvokeSlow<T1, T2>(this Action<T1, T2>[] actions, T1 arg1, T2 arg2, int i, Exception exception)
	{
		var exceptions = new List<Exception> { exception };
		for (; i < actions.Length; i++)
		{
			try
			{
				actions[i](arg1, arg2);
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}
		throw new AggregateException(exceptions.ToArray());
	}

	public static void Invoke<T1, T2, T3>(this Action<T1, T2, T3>[]? actions, T1 arg1, T2 arg2, T3 arg3)
	{
		if (actions is null) return;

		int i = 0;
		try
		{
			for (; i < actions.Length; i++)
			{
				actions[i](arg1, arg2, arg3);
			}
		}
		catch (Exception ex)
		{
			InvokeSlow(actions, arg1, arg2, arg3, i, ex);
		}
	}

	private static void InvokeSlow<T1, T2, T3>(this Action<T1, T2, T3>[] actions, T1 arg1, T2 arg2, T3 arg3, int i, Exception exception)
	{
		var exceptions = new List<Exception> { exception };
		for (; i < actions.Length; i++)
		{
			try
			{
				actions[i](arg1, arg2, arg3);
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}
		throw new AggregateException(exceptions.ToArray());
	}
}

public delegate void RefReadonlyAction<T>(in T obj);
