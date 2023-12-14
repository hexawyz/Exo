using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Programming;

// Type definitions could be user-defined or map to other well-known types of the current model.
[DataContract]
public sealed class TypeDefinition : NamedElement
{
	private static TypeDefinition DeclareIntrinsic(Guid guid, string name, string comment)
		=> new(new(0x70B200C2, 0x8D43, 0x45A0, 0xB2, 0xE7, 0x19, 0x75, 0xC9, 0xED, 0xAB, 0x0C), "int8", "A signed 8-bit integer.", ImmutableArray<FieldDefinition>.Empty);

	public static readonly TypeDefinition Int8 = DeclareIntrinsic(new(0x70B200C2, 0x8D43, 0x45A0, 0xB2, 0xE7, 0x19, 0x75, 0xC9, 0xED, 0xAB, 0x0C), "int8", "A signed 8-bit integer.");
	public static readonly TypeDefinition UInt8 = DeclareIntrinsic(new(0x1DEBFB00, 0x4DA8, 0x4607, 0xBA, 0xF8, 0xA8, 0x29, 0xE0, 0xFA, 0x3B, 0x7B), "uint8", "An unsigned 8-bit integer.");

	public static readonly TypeDefinition Int16 = DeclareIntrinsic(new(0xB074E97A, 0x522E, 0x42E4, 0xA2, 0xCC, 0x16, 0xBC, 0xC8, 0x96, 0x4E, 0x4D), "int16", "A signed 16-bit integer.");
	public static readonly TypeDefinition UInt16 = DeclareIntrinsic(new(0x406F5163, 0x4015, 0x4CFE, 0x90, 0xEB, 0x48, 0xD9, 0x32, 0xBC, 0xBA, 0x7E), "uint16", "An unsigned 16-bit integer.");

	public static readonly TypeDefinition Int32 = DeclareIntrinsic(new(0x39448E8C, 0xA919, 0x483B, 0x85, 0xA4, 0x1D, 0x92, 0xAC, 0x7B, 0x68, 0xC0), "int32", "A signed 32-bit integer.");
	public static readonly TypeDefinition UInt32 = DeclareIntrinsic(new(0x5264AFC6, 0x7CC3, 0x4EE9, 0x8E, 0xF6, 0x62, 0xE3, 0x3C, 0x61, 0x68, 0x51), "uint32", "An unsigned 32-bit integer.");

	public static readonly TypeDefinition Int64 = DeclareIntrinsic(new(0xB9C5FB38, 0x6786, 0x4332, 0x90, 0x3D, 0xDF, 0x04, 0xE0, 0x26, 0xF8, 0xC1), "int64", "A signed 64-bit integer.");
	public static readonly TypeDefinition UInt64 = DeclareIntrinsic(new(0xE1659C12, 0x60D2, 0x41EF, 0xA5, 0xD5, 0x2C, 0x41, 0x3E, 0x32, 0x12, 0xDD), "uint64", "An unsigned 64-bit integer.");

	public static readonly TypeDefinition Float16 = DeclareIntrinsic(new(0x74706EE5, 0x84F0, 0x4977, 0x8C, 0x4D, 0xD8, 0x2D, 0xA3, 0x62, 0x36, 0x80), "float16", "A 16-bit floating point number.");
	public static readonly TypeDefinition Float32 = DeclareIntrinsic(new(0xB568783E, 0x1B6C, 0x4668, 0xAD, 0xD1, 0xBF, 0x19, 0xB4, 0x07, 0xD1, 0x29), "float32", "A 32-bit floating point number.");
	public static readonly TypeDefinition Float64 = DeclareIntrinsic(new(0x380E72E9, 0xC672, 0x493B, 0x9E, 0x86, 0xC5, 0x67, 0xA9, 0x62, 0xEB, 0x49), "float64", "A 64-bit floating point number.");

	public static readonly TypeDefinition Utf8 = DeclareIntrinsic(new(0x5A7EF34F, 0xE300, 0x4890, 0xA6, 0xA4, 0x20, 0x43, 0xD2, 0xF5, 0xCF, 0xAF), "utf8", "A UTF-8 string.");
	public static readonly TypeDefinition Utf16 = DeclareIntrinsic(new(0xF7527537, 0x5170, 0x454E, 0x8B, 0x25, 0x1C, 0x71, 0x27, 0x96, 0x9E, 0xD3), "utf16", "A UTF-16 string.");

	public TypeDefinition(Guid id, string name, string comment, ImmutableArray<FieldDefinition> fields) : base(id, name, comment)
	{
		Fields = fields;
	}

	[DataMember(Order = 4)]
	public ImmutableArray<FieldDefinition> Fields { get; }
}
