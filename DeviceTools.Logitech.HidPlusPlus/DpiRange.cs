using System.Diagnostics;

namespace DeviceTools.Logitech.HidPlusPlus;

[DebuggerDisplay("{Minimum,d} - {Maximum,d} ({Step,d})")]
public readonly struct DpiRange : IEquatable<DpiRange>
{
	public readonly ushort Minimum;
	public readonly ushort Maximum;
	public readonly ushort Step;

	public DpiRange(ushort value) : this(value, value, 0) { }

	public DpiRange(ushort minimum, ushort maximum) : this(minimum, maximum, 1) { }

	public DpiRange(ushort minimum, ushort maximum, ushort step)
		=> (Minimum, Maximum, Step) = (minimum, maximum, step);

	public override bool Equals(object? obj) => obj is DpiRange range && Equals(range);
	public bool Equals(DpiRange other) => Minimum == other.Minimum && Maximum == other.Maximum && Step == other.Step;
	public override int GetHashCode() => HashCode.Combine(Minimum, Maximum, Step);

	public static bool operator ==(DpiRange left, DpiRange right) => left.Equals(right);
	public static bool operator !=(DpiRange left, DpiRange right) => !(left == right);
}
