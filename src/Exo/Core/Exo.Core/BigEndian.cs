using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Exo;

public static class BigEndian
{
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ushort ReadUInt16(in byte source)
		=> BitConverter.IsLittleEndian ?
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in source))) :
			Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in source));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static uint ReadUInt32(in byte source)
		=> BitConverter.IsLittleEndian ?
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(in source))) :
			Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(in source));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void Write(ref byte source, ushort value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void Write(ref byte source, uint value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
}
