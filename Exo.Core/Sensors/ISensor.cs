using System.Numerics;

namespace Exo.Sensors;

public interface ISensor
{
	/// <summary>Gets the well-known unique ID of the sensor.</summary>
	/// <remarks>
	/// This ID should be predetermined and will be used to provide external metadata for a given sensor.
	/// </remarks>
	Guid SensorId { get; }

	/// <summary>Gets the unit of this sensor.</summary>
	Unit Unit { get; }

	/// <summary>Gets the type of values of this sensor.</summary>
	/// <remarks>
	/// Common numeric types are expected here.
	/// Together with <see cref="IsPolled"/>, this property helps determining the actual implementation of the sensor.
	/// </remarks>
	Type ValueType { get; }

	/// <summary>Gets a value indicating whether this sensor is polled.</summary>
	/// <remarks>
	/// Polled sensors will implement the <see cref="IPolledSensor{TValue}"/> interface, while non-polled sensors will implement <see cref="IStreamedSensor{TValue}"/>.
	/// </remarks>
	bool IsPolled { get; }
}

public interface ISensor<TValue> : ISensor
	where TValue : struct, INumber<TValue>
{
	/// <summary>Gets the minimum value expected to be reached by this sensor, if applicable.</summary>
	/// <remarks>
	/// This value should mainly be used for UI purposes.
	/// In some cases, sensor values could go outside the expected limits.
	/// </remarks>
	TValue? ScaleMinimumValue { get; }

	/// <summary>Gets the maximum value expected to be reached by this sensor, if applicable.</summary>
	/// <remarks>
	/// This value should mainly be used for UI purposes.
	/// In some cases, sensor values could go outside the expected limits.
	/// </remarks>
	TValue? ScaleMaximumValue { get; }

	Type ISensor.ValueType => typeof(TValue);
}

public interface IPolledSensor<TValue> : ISensor<TValue>
	where TValue : struct, INumber<TValue>
{
	bool ISensor.IsPolled => true;

	/// <summary>Gets the minimum polling interval to use for querying this sensor.</summary>
	/// <remarks>
	/// <para>
	/// Some sensors may be costly to query, or have a hard limit on how often they can be queried.
	/// This indicates the minimum delay that should be respected between two pollings of the sensor.
	/// </para>
	/// </remarks>
	TimeSpan MinimumPollingInterval { get; }

	/// <summary>Gets the current value of the sensor.</summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask<TValue> GetValueAsync(CancellationToken cancellationToken);
}

public interface IStreamedSensor<TValue> : ISensor<TValue>
	where TValue : struct, INumber<TValue>
{
	bool ISensor.IsPolled => false;

	/// <summary>Enumerates values from the sensor.</summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	IAsyncEnumerable<TValue> EnumerateValuesAsync(CancellationToken cancellationToken);
}

// TODO: This is 💩, but not very important initially. Maybe using the HID unit stuff or something similar would be better.
public enum Unit : short
{
	// Basic units

	Count = 0,
	Probability = 1,
	Percent = 2,

	// Power units

	Volt = 10,
	Ampere = 11,
	Watt = 12,

	// Temperature units
	
	DegreesCelsius = 20,
	DegreesKelvin = 21,
	DegreesFahrenheit = 22,
}
