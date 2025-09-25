using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace DeviceTools.Firmware;

[StructLayout(LayoutKind.Sequential)]
public readonly struct AcpiTableName : IEquatable<AcpiTableName>
{
	private readonly uint _name;

	public AcpiTableName(uint name)
		=> _name = name;

	public override bool Equals(object? obj) => obj is AcpiTableName other && Equals(other);
	public bool Equals(AcpiTableName other) => _name == other._name;
	public override int GetHashCode() => _name.GetHashCode();

	public override string ToString()
		=> string.Create
		(
			sizeof(uint),
			_name,
			(span, name) =>
			{
				if (Sse2.IsSupported)
				{
					Unsafe.As<char, ulong>(ref MemoryMarshal.GetReference(span)) = Sse2.UnpackLow(Vector128.Create(name).AsByte(), Vector128<byte>.Zero).AsUInt64().GetElement(0);
				}
				else
				{
					unsafe
					{
						Encoding.ASCII.GetChars(new Span<byte>((byte*)&name, sizeof(uint)), span);
					}
				}
			}
		);

	public static bool operator ==(AcpiTableName left, AcpiTableName right) => left.Equals(right);
	public static bool operator !=(AcpiTableName left, AcpiTableName right) => !(left == right);
}
