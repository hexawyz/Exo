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

public interface IPolledSensor : ISensor
{
	bool ISensor.IsPolled => true;

	/// <summary>Gets a value indicating the group query mode for this sensor.</summary>
	/// <remarks>
	/// <para>
	/// For devices that support <see cref="Exo.Features.ISensorsGroupedQueryFeature"/>, sensors can be queried as a group, which will often be more efficient than individual queries.
	/// </para>
	/// <para>
	/// When a sensor is registered for grouped querying, <see cref="IPolledSensor{TValue}.GetValueAsync(CancellationToken)"/> will return the value cached by the last read operation.
	/// Otherwise, the sensor will be queried directly.
	/// </para>
	/// </remarks>
	GroupedQueryMode GroupedQueryMode => GroupedQueryMode.None;
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

public interface IPolledSensor<TValue> : ISensor<TValue>, IPolledSensor
	where TValue : struct, INumber<TValue>
{
	/// <summary>Gets the last known value of the sensor.</summary>
	/// <remarks>
	/// <para>
	/// When the sensor is registered for grouped querying, this returns the value of the sensor at the time of the last grouped query.
	/// If a grouped query for the sensor has not yet been executed, for simplicity of implementation,
	/// the method shall return the last read, or the default value for <typeparamref name="TValue"/> if the last value is unknown.
	/// </para>
	/// <para>When the sensor is not registered for grouped querying, it is queried when the function is called.</para>
	/// </remarks>
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

public enum GroupedQueryMode : byte
{
	None = 0,
	Supported = 1,
	Enabled = 2,
}
