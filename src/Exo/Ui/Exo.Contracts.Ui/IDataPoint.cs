using System.Numerics;

namespace Exo.Contracts.Ui;

public interface IDataPoint<TInput, TOutput>
	where TInput : struct, INumber<TInput>
	where TOutput : struct, INumber<TOutput>
{
	public TInput X { get; }
	public TOutput Y { get; }
}
