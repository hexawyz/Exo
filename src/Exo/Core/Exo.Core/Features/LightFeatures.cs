using System.Collections.Immutable;

namespace Exo.Features.Lights;

/// <summary>The feature exposed by a device supporting one or more lights.</summary>
public interface ILightControllerFeature : ILightDeviceFeature
{
	/// <summary>Gets all the individual lights exposed by the current device.</summary>
	ImmutableArray<ILight> Lights { get; }
}

public interface IPolledLightControllerFeature : ILightDeviceFeature
{
	/// <summary>Requests a refresh of the lights.</summary>
	/// <remarks>
	/// <para>
	/// Calling this method notifies the driver of a request for recent data.
	/// The driver is free to decide the right moment to refresh light states.
	/// This method does not imply an immediate refresh of the light states.
	/// Instead, a driver can decide to keep a timestamp of when the last update occurred and decide to ignore refresh requests when appropriate.
	/// The caller must never expect any observable change as a result of calling this method. (It <b>can</b> happen. It is not guaranteed.)
	/// </para>
	/// <para>
	/// Update notifications will be sent through <see cref="ILight{TState}.Changed"/> for each of the affected lights, if there is an actual change observed.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask RequestRefreshAsync(CancellationToken cancellationToken);
}

/// <summary>The base implementation for a light.</summary>
/// <remarks>
/// Lights should implement the appropriate <see cref="ILight{TState}"/> interface with the state that correspond by the feature set supported by the light.
/// Individual features should be supported by implementing their dedicated interface.
/// </remarks>
public interface ILight
{
	/// <summary>Gets an unique ID for the light.</summary>
	/// <remarks>
	/// <para>
	/// Similar to IDs used to other features, this ID is supposed to be well-known and only expected to be unique within a device instance.
	/// It is expected that two identical devices would use the same ID for the same ID.
	/// </para>
	/// <para>That well-known ID will optionally be used to provide more information abut the light in metadata. (For example a graphical illustration)</para>
	/// </remarks>
	Guid Id { get; }
	bool IsOn { get; }
	ValueTask SwitchAsync(bool isOn, CancellationToken cancellationToken);
}

/// <summary>
/// 
/// </summary>
/// <typeparam name="TState">Can be one of the allowed states: <see cref="LightState"/>, <see cref="DimmableLightState"/>, <see cref="TemperatureAdjustableLightState"/> or <see cref="TemperatureAdjustableDimmableLightState"/>.</typeparam>
public interface ILight<TState> : ILight
	where TState : struct, ILightState
{
	event LightChangeHandler<TState> Changed;
	ValueTask UpdateAsync(TState state, CancellationToken cancellationToken);
}

public interface ILightBrightness : ILight
{
	byte Minimum => 0;
	byte Maximum => 100;
	byte Value { get; }
	ValueTask SetBrightnessAsync(byte brightness, CancellationToken cancellationToken);
}

public interface ILightTemperature : ILight
{
	uint Minimum { get; }
	uint Maximum { get; }
	uint Value { get; }
	ValueTask SetTemperatureAsync(uint temperature, CancellationToken cancellationToken);
}

public delegate void LightChangeHandler<TState>(Driver driver, TState state)
	where TState : struct, ILightState;

public interface ILightState
{
	bool IsOn { get; }
}

public readonly struct LightState(bool isOn) : ILightState
{
	public bool IsOn { get; } = isOn;
}

public readonly struct DimmableLightState(bool isOn, byte brightness) : ILightState
{
	public bool IsOn { get; } = isOn;
	public byte Brightness { get; } = brightness;
}

public readonly struct TemperatureAdjustableLightState(bool isOn, uint temperature) : ILightState
{
	public bool IsOn { get; } = isOn;
	public uint Temperature { get; } = temperature;
}

public readonly struct TemperatureAdjustableDimmableLightState(bool isOn, byte brightness, uint temperature) : ILightState
{
	public bool IsOn { get; } = isOn;
	public byte Brightness { get; } = brightness;
	public uint Temperature { get; } = temperature;
}
