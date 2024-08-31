using System.Collections.Immutable;
using Exo.Sensors;

namespace Exo.Features.Sensors;

/// <summary>Exposes the sensors of a sensors device.</summary>
/// <remarks>Sensor features are assumed to not be assumed concurrently unless specified otherwise.</remarks>
public interface ISensorsFeature : ISensorDeviceFeature
{
	/// <summary>Gets the sensors exposed by the device.</summary>
	/// <remarks>
	/// <para>
	/// This value should never change during the lifetime of a device, as the sensors supported by a device is expected to be a constant property.
	/// </para>
	/// </remarks>
	ImmutableArray<ISensor> Sensors { get; }
}

/// <summary>A feature that allows polling the values for multiple sensors at once.</summary>
/// <remarks>
/// <para>
/// Sensor features are assumed to not be assumed concurrently unless specified otherwise.
/// </para>
/// <para>
/// For some devices, the cost of querying sensors can be amortized if multiple sensors are queried at once.
/// This may be the case if some internal queries return the values for multiple sensors at once, or if accessing the device implies some specific requirement.
/// e.g. Some well-known devices are expected to be accessed from multiple services on the same computer, and as such may require acquiring lock and executing a specific set of operations in order to
/// ensure that the device is in a well-known state before even querying a single sensor.
/// </para>
/// <para>
/// If a device supports grouped querying, it should generally always be used.
/// This interface, however, allows picking which sensors are part of the grouped query in the likely case where only a subset of sensor should be queried.
/// </para>
/// <para>Consumers of this interface (the sensor service) is assumed to be aware of the sensors that are currently registered.</para>
/// </remarks>
public interface ISensorsGroupedQueryFeature : ISensorDeviceFeature
{
	/// <summary>Adds a sensor to the grouped query.</summary>
	/// <remarks>The value for the added sensor will be queried on the next execution of <see cref="QueryValuesAsync(CancellationToken)"/>.</remarks>
	/// <param name="sensor">The sensor to add.</param>
	/// <exception cref="ArgumentException">The sensor is not associated with the device.</exception>
	/// <exception cref="InvalidOperationException">The sensor does not support grouped querying.</exception>
	void AddSensor(IPolledSensor sensor);

	/// <summary>Removes a sensor from the grouped query.</summary>
	/// <param name="sensor"></param>
	/// <exception cref="ArgumentException">The sensor is not associated with the device.</exception>
	void RemoveSensor(IPolledSensor sensor);

	/// <summary>Queries the values of the currently registered sensors.</summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask QueryValuesAsync(CancellationToken cancellationToken);
}
