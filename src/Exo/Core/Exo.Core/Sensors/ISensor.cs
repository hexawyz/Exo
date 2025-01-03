using System.Numerics;

namespace Exo.Sensors;

public enum SensorKind
{
	Internal,
	Polled,
	Streamed,
}

public interface ISensor
{
	/// <summary>Gets the well-known unique ID of the sensor.</summary>
	/// <remarks>
	/// This ID should be predetermined and will be used to provide external metadata for a given sensor.
	/// </remarks>
	Guid SensorId { get; }

	/// <summary>Gets the unit of this sensor.</summary>
	/// <remarks>
	/// This is likely to evolve in the future, or to be removed from the interface itself.
	/// The unit should be deductible from the metadata attached to <see cref="SensorId"/>, but until this is implemented, it is simpler to expose it here.
	/// </remarks>
	SensorUnit Unit { get; }

	/// <summary>Gets the type of values of this sensor.</summary>
	/// <remarks>
	/// Common numeric types are expected here.
	/// Together with <see cref="IsPolled"/>, this property helps determining the actual implementation of the sensor.
	/// </remarks>
	Type ValueType { get; }

	/// <summary>Gets a value indicating the kind of sensor.</summary>
	/// <remarks>
	/// <para>
	/// Internal sensors will implement <see cref="IInternalSensor{TValue}"/>, polled sensors will implement the <see cref="IPolledSensor{TValue}"/> interface,
	/// while non-polled sensors will implement <see cref="IStreamedSensor{TValue}"/>.
	/// </para>
	/// <para>
	/// Internal sensors are sensors that exist within the device and can be used for some other features such as hardware cooling curves, but are not exposed directly by the device.
	/// </para>
	/// </remarks>
	SensorKind Kind { get; }
}

/// <summary>Represents a sensor that cannot be queried.</summary>
public interface IInternalSensor : ISensor
{
	SensorKind ISensor.Kind => SensorKind.Internal;
}

public interface IPolledSensor : ISensor
{
	SensorKind ISensor.Kind => SensorKind.Polled;

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

/// <summary>Represents a sensor that cannot be queried.</summary>
public interface IInternalSensor<TValue> : ISensor<TValue>, IInternalSensor
	where TValue : struct, INumber<TValue>
{
}

public interface IPolledSensor<TValue> : ISensor<TValue>, IPolledSensor
	where TValue : struct, INumber<TValue>
{
	/// <summary>Gets the value of the sensor.</summary>
	/// <remarks>
	/// <para>
	/// When the sensor is registered for grouped querying, this returns the value of the sensor at the time of the last grouped query,
	/// which corresponds to the value that would be returned by <see cref="TryGetLastValue(out TValue)"/>.
	/// If a grouped query for the sensor has not yet been executed, for simplicity of implementation,
	/// the method shall return the last read, or the default value for <typeparamref name="TValue"/> if the last value is unknown.
	/// </para>
	/// <para>When the sensor is not registered for grouped querying, it is queried when the function is called.</para>
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask<TValue> GetValueAsync(CancellationToken cancellationToken);

	/// <summary>Tries to get the last known value.</summary>
	/// <remarks>
	/// <para>
	/// This is a faster way to read a value from the sensor, if it supports it.
	/// This is mainly intended for use for grouped queries, but sensors can choose to always expose the last read value here, without freshness guarantee.
	/// </para>
	/// <para>
	/// Sensors that are taking part in a grouped query must <b>always</b> provide their own implementation of this method, and return the value read by the last grouped query.
	/// </para>
	/// </remarks>
	/// <param name="lastValue"></param>
	/// <returns></returns>
	bool TryGetLastValue(out TValue lastValue)
	{
		lastValue = default;
		return false;
	}
}

public interface IStreamedSensor<TValue> : ISensor<TValue>
	where TValue : struct, INumber<TValue>
{
	SensorKind ISensor.Kind => SensorKind.Streamed;

	/// <summary>Enumerates values from the sensor.</summary>
	/// <remarks>Implementations can assume that there will always be at most a single enumeration per sensor at a given time.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	IAsyncEnumerable<SensorDataPoint<TValue>> EnumerateValuesAsync(CancellationToken cancellationToken);
}

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


public enum GroupedQueryMode : byte
{
	None = 0,
	Supported = 1,
	Enabled = 2,
}

public readonly struct SensorUnit
{
	public static readonly SensorUnit None = new("");
	public static readonly SensorUnit Percent = new("%");
	public static readonly SensorUnit Volts = new("V");
	public static readonly SensorUnit Amperes = new("A");
	public static readonly SensorUnit Watts = new("W");
	public static readonly SensorUnit Joules = new("J");
	public static readonly SensorUnit Celsius = new("°C");
	public static readonly SensorUnit Fahrenheits = new("°F");
	public static readonly SensorUnit RotationsPerMinute = new("RPM");
	public static readonly SensorUnit Hertz = new("Hz");
	public static readonly SensorUnit KiloHertz = new("kHz");
	public static readonly SensorUnit MegaHertz = new("MHz");
	public static readonly SensorUnit GigaHertz = new("GHz");

	public string Symbol { get; }

	private SensorUnit(string symbol) => Symbol = symbol;
}
