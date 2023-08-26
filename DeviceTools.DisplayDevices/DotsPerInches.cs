using System;

namespace DeviceTools.DisplayDevices;

public readonly struct DotsPerInch : IEquatable<DotsPerInch>
{
	public DotsPerInch(uint value) : this(value, value) { }

	public DotsPerInch(uint horizontal, uint vertical)
	{
		Horizontal = horizontal;
		Vertical = vertical;
	}

	public uint Horizontal { get; }
	public uint Vertical { get; }

	public override bool Equals(object? obj) => obj is DotsPerInch inch && Equals(inch);
	public bool Equals(DotsPerInch other) => Horizontal == other.Horizontal && Vertical == other.Vertical;
	public override int GetHashCode() => HashCode.Combine(Horizontal, Vertical);

	public static bool operator ==(DotsPerInch left, DotsPerInch right) => left.Equals(right);
	public static bool operator !=(DotsPerInch left, DotsPerInch right) => !(left == right);
}
