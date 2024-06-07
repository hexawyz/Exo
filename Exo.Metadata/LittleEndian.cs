using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Exo;

internal static class LittleEndian
{
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ushort ReadUInt16(in byte source)
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in source)) :
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in source)));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static uint ReadUInt32(in byte source)
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(in source)) :
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(in source)));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ulong ReadUInt64(in byte source)
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef(in source)) :
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef(in source)));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static float ReadSingle(in byte source)
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<float>(ref Unsafe.AsRef(in source)) :
			Unsafe.BitCast<uint, float>(BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(in source))));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static double ReadDouble(in byte source)
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<double>(ref Unsafe.AsRef(in source)) :
			Unsafe.BitCast<ulong, double>(BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef(in source))));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void Write(ref byte source, ushort value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void Write(ref byte source, uint value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void Write(ref byte source, ulong value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void Write(ref byte source, float value)
	{
		if (BitConverter.IsLittleEndian) Unsafe.WriteUnaligned(ref source, value);
		else Write(ref source, Unsafe.BitCast<float, uint>(value));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void Write(ref byte source, double value)
	{
		if (BitConverter.IsLittleEndian) Unsafe.WriteUnaligned(ref source, value);
		else Write(ref source, Unsafe.BitCast<double, ulong>(value));
	}
}
