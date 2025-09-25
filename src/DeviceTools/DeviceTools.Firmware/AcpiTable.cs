using System.Diagnostics;

namespace DeviceTools.Firmware;

[DebuggerDisplay("{ToDebugString()}")]
public readonly struct AcpiTable
{
	public AcpiTable(AcpiTableName name, uint index, byte[] data)
	{
		Name = name;
		Index = index;
		Data = data;
	}

	public AcpiTableName Name { get; }
	public uint Index { get; }
	public byte[] Data { get; }

	private string ToDebugString()
		=> Index == 0 ? $"{Name}[{Data.Length}]" : $"{Name}{Index}[{Data.Length}]";
}
