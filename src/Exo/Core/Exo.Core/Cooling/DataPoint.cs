using System.Numerics;

namespace Exo.Cooling;

public readonly record struct DataPoint<TX, TY>
	where TX : struct, INumber<TX>
	where TY : struct, INumber<TY>
{
	public DataPoint(TX x, TY y) => (X, Y) = (x, y);

	public TX X { get; init; }
	public TY Y { get; init; }
}
