using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Exo.Cooling;

/// <summary>Exposes a cooler or cooling zone controlled by a device.</summary>
/// <remarks>
/// <para>
/// Classes implementing this interface must also implement at least one of <see cref="IManualCooler"/> and <see cref="IHardwareCurveCooler{T}"/>.
/// Whenever automatic cooling mode is supported (i.e. using a default, non-configurable curve), classes should also implement <see cref="IAutomaticCooler"/>.
/// </para>
/// <para>
/// Changes to the cooler will be applied when <see cref="Exo.Features.Cooling.ICoolingControllerFeature.ApplyChangesAsync"/> is called, and not before that.
/// This allows calls to the device to be efficiently batched, in order to provide better performance, and reduce possible conflicts with other software.
/// </para>
/// <para>
/// Is is very common for multiple fans to be controlled by a single power setting.
/// This interface does not distinguish between a single fan or multiple fans. However, the <see cref="CoolerId"/> should allow to provide that metadata on the cooler if necessary.
/// </para>
/// </remarks>
public interface ICooler
{
	/// <summary>Gets the ID for this cooler.</summary>
	Guid CoolerId { get; }
	/// <summary>Gets the associated sensor ID, if any.</summary>
	/// <remarks>
	/// <para>
	/// While coolers are controlled in percentage, devices will often expose the speed of their coolers in RPM via sensors.
	/// When this is the case, the ID of the sensor associated with a cooler should be returned by this property.
	/// </para>
	/// <para>
	/// NB: While many devices also provide a means of reading the set cooler speed, it would usually not make sense to expose this as a sensor, as those values are configured manually.
	/// </para>
	/// </remarks>
	Guid? SpeedSensorId { get; }
	/// <summary>Gets the type of cooler.</summary>
	CoolerType Type { get; }
	/// <summary>Gets the cooling mode that has been applied.</summary>
	/// <remarks>
	/// With exception of <see cref="CoolingMode.Automatic"/> which is allowed to be the initial "unknown" state of a cooler,
	/// the value returned by this property must correspond to interfaces implemented by the cooler, and reflect recent changes.
	/// A call to <see cref="IAutomaticCooler.SwitchToAutomaticCooling"/> should change this value to <see cref="CoolingMode.Automatic"/>.
	/// A call to <see cref="IManualCooler.SetPower"/> should change this value to <see cref="CoolingMode.Manual"/>.
	/// </remarks>
	CoolingMode CoolingMode { get; }
}

public interface IAutomaticCooler
{
	/// <summary>Sets the cooling mode to automatic.</summary>
	/// <returns></returns>
	void SwitchToAutomaticCooling();
}

public interface IConfigurableCooler
{
	/// <summary>Gets the minimum power that can be assigned to a cooler.</summary>
	/// <remarks>
	/// <para>
	/// This will often be <c>0</c>, but some coolers will forbid going below a certain value, e.g. 30%.
	/// Such coolers can additionally support being switched off by setting the power target to <c>0</c>, which will be indicated by <see cref="CanSwitchOff"/> returning true.
	/// </para>
	/// <para>
	/// A minimum power value of <c>0</c>, always indicate that the cooler can be switched off. In that case, <see cref="CanSwitchOff"/> must be <see langword="true"/>.
	/// </para>
	/// </remarks>
	byte MinimumPower { get; }
	/// <summary>Gets a value indicating if this cooler can be switched off.</summary>
	/// <remarks>When this value is <see langword="true"/>, the cooler can be switched off by setting the power to <c>0</c>.</remarks>
	bool CanSwitchOff { get; }
}

public interface IManualCooler : IConfigurableCooler
{
	/// <summary>Sets the power target for this cooler.</summary>
	/// <remarks>A call to this method must change <see cref="ICooler.CoolingMode"/> to <see cref="CoolingMode.Manual"/>.</remarks>
	/// <param name="power">The power target.</param>
	/// <returns></returns>
	void SetPower(byte power);
	/// <summary>Tries to get the last power target configured for the cooler.</summary>
	/// <remarks>When the device is not in <see cref="CoolingMode.Manual"/> cooling mode, this method must return <see langword="false"/>.</remarks>
	/// <param name="power">Power target that was last configured.</param>
	/// <returns><see langword="true"/> if the device was in manual cooling mode and the power could be returned; <see langword="false"/> otherwise.</returns>
	bool TryGetPower(out byte power);
}

public interface IHardwareCurveCooler : IConfigurableCooler
{
	/// <summary>Gets a list of all available input sensors.</summary>
	/// <remarks>Currently the sensors must be present in a readable form on the device, because some informations are missing in external metadata. This can be alleviated later.</remarks>
	ImmutableArray<Guid> AvailableInputSensors { get; }
}

public interface IHardwareCurveCooler<T> : IHardwareCurveCooler
	where T: struct, INumber<T>
{
	/// <summary>Sets the control curve to be used by this cooler.</summary>
	/// <remarks>
	/// <para>Only the sensors referenced by <see cref="AvailableInputSensors"/> can be used as control curve input.</para>
	/// <para>
	/// Upon receiving a valid request, the driver shall reinterpret the curve so as to fit the internal data model that suits the device.
	/// Drivers are in no way expected to preserve the control curve reference provided as an input, and callers should ot assume that the data can be read back again.
	/// Drivers are expected to return the active curve upon a call to <see cref="TryGetControlCurve(out Guid, out IControlCurve{T, byte}?)"/>,
	/// using data that fit their internal representation the best.
	/// </para>
	/// </remarks>
	/// <param name="inputId">The sensor to use as an input.</param>
	/// <param name="curve">The control curve to apply.</param>
	void SetControlCurve(Guid inputId, IControlCurve<T, byte> curve);

	/// <summary>Gets the applied cooling curve.</summary>
	/// <remarks>
	/// The curve returned can be different than the one provided to <see cref="SetControlCurve(Guid, IControlCurve{T, byte})"/> in an earlier call.
	/// For simplicity, device drivers are allowed to return the curve that best matches their internal representation, meaning that data points can be truncated or interpolated,
	/// and values can be rounded accordingly.
	/// </remarks>
	/// <param name="inputId">The sensor used as input of this source, if any.</param>
	/// <param name="curve">The curve currently applied on the cooler, if any.</param>
	/// <returns></returns>
	bool TryGetControlCurve(out Guid inputId, [NotNullWhen(true)] out IControlCurve<T, byte>? curve);
}

public readonly struct HardwareCoolingInput
{
	/// <summary>Gets the built-in sensor ID, if any is available.</summary>
	public Guid SensorId { get; }
}

/// <summary>Identifies the type of a cooler.</summary>
/// <remarks>
/// When a cooler is a <see cref="CoolerType.Fan"/>, it could represent more than one fan.
/// This does, however, not impact the cooling API, so that information is left to be provided in separate metadata if it is necessary.
/// </remarks>
public enum CoolerType
{
	Other = 0,
	Fan = 1,
	Pump = 2,
}

/// <summary>Identifies the cooling mode of a cooler.</summary>
/// <remarks>
/// The members of this enumeration represent cooling modes commonly supported by devices.
/// More modes can be added in the future as necessary. (e.g. specific preset modes)
/// </remarks>
public enum CoolingMode
{
	Automatic = 0,
	Manual = 1,
	HardwareControlCurve = 2,
}
