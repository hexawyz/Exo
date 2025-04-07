using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProtoBuf;
using ProtoBuf.Serializers;

namespace Exo.Contracts.Ui;

internal class UInt128Serializer : ProtoBuf.Serializers.ISerializer<UInt128>
{
	public SerializerFeatures Features => SerializerFeatures.CategoryRepeated | SerializerFeatures.CategoryScalar | SerializerFeatures.WireTypeString;

	public UInt128 Read(ref ProtoReader.State state, UInt128 value)
	{
		Unsafe.SkipInit(out value);
		var result = state.ReadBytes(MemoryMarshal.Cast<UInt128, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
		if (result.Length != Unsafe.SizeOf<UInt128>()) throw new InvalidDataException();
		return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
	}

	public void Write(ref ProtoWriter.State state, UInt128 value)
	{
		if (!BitConverter.IsLittleEndian) value = BinaryPrimitives.ReverseEndianness(value);
		state.WriteBytes(MemoryMarshal.Cast<UInt128, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
	}
}
