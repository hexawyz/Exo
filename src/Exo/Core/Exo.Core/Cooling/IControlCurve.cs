using System.Numerics;

namespace Exo.Cooling;

public interface IControlCurve<TInput, TOutput>
	where TInput : struct, INumber<TInput>
	where TOutput : struct, INumber<TOutput>
{
	TOutput this[TInput value] { get; }
}
