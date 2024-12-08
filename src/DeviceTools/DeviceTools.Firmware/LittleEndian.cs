using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Firmware;

// This is a relatively safe internal helper used to read values from potentially unaligned places.
// Only intrinsic integral types, enums, and the Guid type are explicitly supported.
// Trying to use these helper methods with a custom data type may produce invalid results. (That's why it is not 100% safe)
internal static class LittleEndian
{
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static T ReadAt<T>(in T source) where T : unmanaged => ReverseEndiannessIfNeededAndNativeSize(Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in source))));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static T Read<T>(ReadOnlySpan<byte> span) where T : unmanaged
	{
		// The Guid type already contains the logic to decode a Guid assuming Little Endian byte order, so we directly rely on it when needed.
		if (!BitConverter.IsLittleEndian && typeof(T) == typeof(Guid))
		{
			return As<Guid, T>(new Guid(span[..16]));
		}

		return ReverseEndiannessIfNeededAndNativeSize(Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(span[..Unsafe.SizeOf<T>()])));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static T ReverseEndiannessIfNeededAndNativeSize<T>(T value)
	{
		if (BitConverter.IsLittleEndian) return value;

		if (typeof(T) == typeof(Guid)) return ReverseEndiannessOfGuid(value);

		return ReverseEndiannessIfNativeSize(value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static T ReverseEndiannessIfNativeSize<T>(T value)
	{
		if (Unsafe.SizeOf<T>() == 1) return value;

		if (Unsafe.SizeOf<T>() == 2) return As<ushort, T>(BinaryPrimitives.ReverseEndianness(As<T, ushort>(value)));
		if (Unsafe.SizeOf<T>() == 4) return As<uint, T>(BinaryPrimitives.ReverseEndianness(As<T, uint>(value)));
		if (Unsafe.SizeOf<T>() == 8) return As<ulong, T>(BinaryPrimitives.ReverseEndianness(As<T, ulong>(value)));

		return value;
	}

	private static T ReverseEndiannessOfGuid<T>(in T value) => As<Guid, T>(new Guid(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in value)), 16)));

	private static TTo As<TFrom, TTo>(TFrom value) => Unsafe.As<TFrom, TTo>(ref value);
}
