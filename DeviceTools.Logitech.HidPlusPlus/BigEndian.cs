using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace DeviceTools.Logitech.HidPlusPlus;

internal static class BigEndian
{
	public static ushort ReadUInt16(in byte source)
		=> BitConverter.IsLittleEndian ?
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(source))) :
			Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(source));

	public static uint ReadUInt32(in byte source)
		=> BitConverter.IsLittleEndian ?
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(source))) :
			Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(source));

	public static void Write(ref byte source, ushort value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);

	public static void Write(ref byte source, uint value)
		=> Unsafe.WriteUnaligned(ref source, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
}
