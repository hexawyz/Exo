using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Exo.Settings.Ui;

internal static class ArrayExtensions
{
	[DebuggerStepThrough]
	[DebuggerHidden]
	public static ImmutableArray<T> AsImmutable<T>(this T[] array) => Unsafe.As<T[], ImmutableArray<T>>(ref array);

	[DebuggerStepThrough]
	[DebuggerHidden]
	public static T[] AsMutable<T>(this ImmutableArray<T> array) => Unsafe.As<ImmutableArray<T>, T[]>(ref array);
}
