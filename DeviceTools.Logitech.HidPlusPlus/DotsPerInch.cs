using System.Diagnostics;

namespace DeviceTools.Logitech.HidPlusPlus;

[DebuggerDisplay("{Horizontal,d}x{Vertical,d}")]
public readonly struct DotsPerInch : IEquatable<DotsPerInch>
{
	public DotsPerInch(ushort value) : this(value, value) { }

	public DotsPerInch(ushort horizontal, ushort vertical)
	{
		Horizontal = horizontal;
		Vertical = vertical;
	}

	public ushort Horizontal { get; }
	public ushort Vertical { get; }

	public override bool Equals(object? obj) => obj is DotsPerInch inch && Equals(inch);
	public bool Equals(DotsPerInch other) => Horizontal == other.Horizontal && Vertical == other.Vertical;
	public override int GetHashCode() => HashCode.Combine(Horizontal, Vertical);

	public static bool operator ==(DotsPerInch left, DotsPerInch right) => left.Equals(right);
	public static bool operator !=(DotsPerInch left, DotsPerInch right) => !(left == right);
}
