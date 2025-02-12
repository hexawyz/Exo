namespace Exo.Settings.Ui;

internal readonly struct UnsignedRationalNumber16
{
	public static UnsignedRationalNumber16 One => new(1, 1);

	public static UnsignedRationalNumber16 Reduce(ushort p, ushort q)
	{
		ArgumentOutOfRangeException.ThrowIfZero(q);
		uint gcd = MathUtils.Gcd(p, q);
		return new((ushort)(p / gcd), (ushort)(q / gcd));
	}

	public ushort P { get; }
	public ushort Q { get; }

	public UnsignedRationalNumber16(ushort p, ushort q)
	{
		ArgumentOutOfRangeException.ThrowIfZero(q);
		P = p;
		Q = q;
	}
}
