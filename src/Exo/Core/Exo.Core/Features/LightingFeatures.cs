using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Features.Lighting;

/// <summary>The feature exposed by a lighting controller supporting one or more light zones.</summary>
/// <remarks>This is optional for devices supporting only a single light zone</remarks>
public interface ILightingControllerFeature : ILightingDeviceFeature
{
	/// <summary>Gets all the individual lighting zones exposed by the current device.</summary>
	/// <remarks>
	/// The returned collection must not include the unified lighting zone exposed by <see cref="IUnifiedLightingFeature"/>, or any zone that would be the composite of two other zones.
	/// Having composite zones other than the unified lighting zone is currently unsupported. Although it could make sense in some cases, we probably don't need that complexity for now.
	/// </remarks>
	/// <returns></returns>
	IReadOnlyCollection<ILightingZone> LightingZones { get; }
}

public interface ILightingPersistenceMode : ILightingDeviceFeature
{
	/// <summary>Indicates how the device will persist the lighting changes.</summary>
	/// <remarks>Availability of on-demand lighting persistence is not guaranteed. This property will indicate the capabilities of the device regarding this.</remarks>
	LightingPersistenceMode PersistenceMode { get; }
}

/// <summary>A feature allowing to apply deferred lighting changes to a device.</summary>
/// <remarks>
/// <para>
/// While not strictly mandatory, lighting drivers should implement a deferred changes mode to allow for more predictable operation.
/// This features provides the <see cref="ApplyChangesAsync"/> method that will apply all the pending changes.
/// </para>
/// <para>In the absence of this feature, it is assumed that lighting changes are applied immediately.</para>
/// </remarks>
public interface ILightingDeferredChangesFeature : ILightingDeviceFeature, ILightingPersistenceMode
{
	/// <summary>Applies changes to the current lighting effects.</summary>
	/// <remarks>
	/// This can optionally persist the applied lighting settings on the device.
	/// The parameter <paramref name="shouldPersist"/> will be silently ignored if persistence can not be chosen on the device.
	/// </remarks>
	/// <param name="shouldPersist">Indicates if the driver should try to persist the changes in device non-volatile memory.</param>
	ValueTask ApplyChangesAsync(bool shouldPersist);
}

/// <summary>A feature that allows controlling a device as a single unified lighting zone.</summary>
/// <remarks>
/// <para>
/// Most devices should provide this feature, especially devices that can provide centralized light themes.
/// </para>
/// <para>
/// When lighting is controlled in an unified way, all individual lighting zones must return <see cref="NotApplicableEffect"/>.
/// Conversely, when lighting is controlled in a non-unified way, the unified lighting zone must return <see cref="NotApplicableEffect"/>.
/// Switching between unified lighting and non-unified lighting should be as simple as setting a lighting effect on either the unified lighting zone or an individual lighting zone.
/// Settings still need to be applied for the change to be observed on the device.
/// </para>
/// </remarks>
public interface IUnifiedLightingFeature : ILightingDeviceFeature, ILightingZone
{
	/// <summary>Gets a value indicating whether lighting is currently unified on the device.</summary>
	bool IsUnifiedLightingEnabled { get; }
}

/// <summary>A feature allowing to update the brightness level used for lighting effects.</summary>
/// <remarks>
/// <para>
/// Most lighting devices have a brightness setting that is used when displaying colors of predefined effects.
/// This setting is mostly useful for effects whose colors can't be defined by the user, but they would generally apply to all non-dynamic effects.
/// </para>
/// <para>Effects implementing <see cref="IBrightnessLightingEffect"/> will override the default brightness, but use the same minimum and maximum values.</para>
/// </remarks>
public interface ILightingBrightnessFeature : ILightingDeviceFeature
{
	/// <summary>Get the minimum brightness level.</summary>
	/// <remarks>
	/// <para>
	/// This value represents the minimum non-null valid brightness for the device.
	/// For all intents and purposes, the technical minimum brightness will always be <c>0</c>, but setting the brightness to <c>0</c> would result in a disabled effect.
	/// </para>
	/// <para>Most implementations should return the default value of <c>1</c> here.</para>
	/// <para>The technical minimum brightness is always considered to be <c>0</c>, but setting the value <c>0</c> is generally not practical, except in certain circumstances.</para>
	/// </remarks>
	byte MinimumBrightness => 1;

	/// <summary>Get the maximum brightness level.</summary>
	/// <remarks>
	/// <para>
	/// This value generally corresponds to the number of brightness levels the device supports.
	/// It can be used to compute the brightness percentage.
	/// </para>
	/// <para>
	/// Generally, devices will support setting 100 or 255 levels of brightness, but some devices may use more unusual values.
	/// Brightness values could always be abstracted to <c>100%</c> but it is more helpful to surface the ticks in the UI when possible.
	/// </para>
	/// </remarks>
	byte MaximumBrightness { get; }

	/// <summary>Gets or sets the current brightness level.</summary>
	/// <remarks>The brightness value must be between <see cref="MinimumBrightness"/> and <see cref="MaximumBrightness"/> inclusive.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">The <paramref name="brightness"/> parameter is out of range.</exception>
	byte CurrentBrightness { get; set; }

	/// <summary>Gets the brightness as a percentage.</summary>
	float BrightnessPercentage => CurrentBrightness / (float)MaximumBrightness;
}

/// <summary>A feature allowing to notify effect changes.</summary>
/// <remarks>
/// This feature is intended to support lighting controllers who are expected to change outside of the control of the service.
/// This will be the case of devices supporting physical controls or devices that are commonly accessed through multiple services or devices, which is the case of Elgato lights.
/// Most drivers <em>should not</em> implement this feature, especially as it will make the logic more complex for no benefit at all.
/// </remarks>
public interface ILightingDynamicChanges : ILightingDeviceFeature
{
	/// <summary>Indicates whether the effects persistence is managed on device side.</summary>
	/// <remarks>
	/// <para>
	/// Most devices will have their effects managed on the software side, but devices having readable external lighting changes
	/// may benefit from being left alone managing their state.
	/// In particular, letting the device manage its own state will help reduce the wear and tear experienced by the device, by
	/// avoiding overriding the current effect with one stored in the software.
	/// </para>
	/// <para>
	/// This is somewhat strongly associated with <see cref="HasDynamicPresence"/>, as the combination of both flags will indicate to the software
	/// if the lighting effects are to be stored on disk whenever they change.
	/// </para>
	/// </remarks>
	bool HasDeviceManagedLighting { get; }
	/// <summary>Indicates whether the device presence should be managed dynamically.</summary>
	/// <remarks>
	/// <para>
	/// By default, lighting devices are considered permanent and their effects can be adjusted even when the device is offline.
	/// Devices whose presence is more situational, especially devices that are external to the computer, can benefit from not being treated this way.
	/// In particular, this can alleviate the software from being required to store lighting effects on disk everytime they are changed.
	/// </para>
	/// <para>
	/// This is somewhat strongly associated with <see cref="HasDeviceManagedLighting"/>, as the combination of both flags will indicate to the software
	/// if the lighting effects are to be stored on disk whenever they change.
	/// </para>
	/// </remarks>
	bool HasDynamicPresence { get; }
	/// <summary>Notifies that the effect has changed on a specific lighting zone.</summary>
	/// <remarks>This event does not indicate whether the change was externally triggered.</remarks>
	event EffectChangeHandler EffectChanged;
}

public delegate void EffectChangeHandler(ILightingZone zone, ILightingEffect effect);
