using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Exo;

public static class LittleEndian
{
	public static ushort ReadUInt16(in byte source)
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(source)) :
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(source)));

	public static uint ReadUInt32(in byte source)
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(source)) :
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(source)));

	public static void Write(ref byte source, ushort value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value));

	public static void Write(ref byte source, uint value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value));
}
