using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Exo.Service;

internal static class ArrayExtensions
{
	public static int Length<T>(this ImmutableArray<T> array)
		=> !array.IsDefault ? array.Length : 0;

	public static int GetHashCode<T>(this ImmutableArray<T> array)
		=> GetHashCode(ImmutableCollectionsMarshal.AsArray(array)!);

	public static int GetHashCode<T>(this T[] array)
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

	public static T[] Add<T>(this T[]? array, T item)
	{
		if (array is null) return new T[] { item };

		Array.Resize(ref array, array.Length + 1);
		array[^1] = item;

		return array;
	}

	public static T[]? Remove<T>(this T[]? array, T item)
	{
		if (array is null) return null;

		if (Array.IndexOf(array, item) is int index and >= 0)
		{
			int newLength = array.Length - 1;

			if (index < newLength)
			{
				Array.Copy(array, index + 1, array, index, newLength - index);
			}

			Array.Resize(ref array, newLength);
		}

		return array;
	}

	public static void InterlockedAdd<T>(ref T[]? array, T item)
	{
		var a = array;
		while (true)
		{
			if (a == (a = Interlocked.CompareExchange(ref array, a.Add(item), a)))
			{
				return;
			}
		}
	}

	public static void InterlockedRemove<T>(ref T[]? array, T item)
	{
		var a = array;
		while (true)
		{
			if (a == (a = Interlocked.CompareExchange(ref a, a.Remove(item), a)))
			{
				return;
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

	public static void TryWrite<T>(this ChannelWriter<T>[]? writers, T item)
	{
		if (writers is null) return;
		foreach (var writer in writers)
		{
			writer.TryWrite(item);
		}
	}
}

internal delegate void RefReadonlyAction<T>(in T obj);
