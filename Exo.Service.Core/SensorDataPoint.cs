using System.Numerics;

namespace Exo.Service;

public readonly struct SensorDataPoint<TValue>
	where TValue : struct, INumber<TValue>
{
	public SensorDataPoint(DateTime dateTime, TValue value)
	{
		DateTime = dateTime;
		Value = value;
	}

	public DateTime DateTime { get; }
	public TValue Value { get; }
}
