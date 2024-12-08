using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools
{
	internal static class FixedLengthArray
	{
#if NETSTANDARD2_0
		// I don't believe there is a fast, reliable and (memory-)efficient way of doing this on pure .NET standard 2.0.
		// So… we'll allocate and copy… Anyway, people should rely on the most recent version of the lib.
		private static ReadOnlySpan<TElement> AsSpan<T, TElement>(in T array, int length)
			where T : struct
		{
			var result = new TElement[length];

			ref TElement current = ref Unsafe.As<T, TElement>(ref Unsafe.AsRef(array));
			for (int i = 0; i < length; i++)
			{
				result[i] = current;
				current = ref Unsafe.Add(ref current, 1);
			}
			return result.AsSpan();
		}
#else
		private static ReadOnlySpan<TElement> AsSpan<T, TElement>(in T array, int length)
			where T : struct =>
			MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, TElement>(ref Unsafe.AsRef(in array)), length);
#endif

		public static ReadOnlySpan<T> AsSpan<T>(in FixedLengthArray2<T> array) => AsSpan<FixedLengthArray2<T>, T>(array, 2);
		public static ReadOnlySpan<T> AsSpan<T>(in FixedLengthArray3<T> array) => AsSpan<FixedLengthArray3<T>, T>(array, 3);
		public static ReadOnlySpan<T> AsSpan<T>(in FixedLengthArray4<T> array) => AsSpan<FixedLengthArray4<T>, T>(array, 4);
	}
}
