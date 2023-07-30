using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Exo.Service;

internal static class ArrayExtensions
{
	public static ImmutableArray<T> AsImmutable<T>(this T[] array) => Unsafe.As<T[], ImmutableArray<T>>(ref array);

	public static T[] AsMutable<T>(this ImmutableArray<T> array) => Unsafe.As<ImmutableArray<T>, T[]>(ref array);

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
