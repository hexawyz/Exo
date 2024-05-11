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

/// <summary>A feature allowing to apply deferred lighting changes to a device.</summary>
/// <remarks>
/// <para>
/// While not strictly mandatory, lighting drivers should implement a deferred changes mode to allow for more predictable operation.
/// This features provides the <see cref="ApplyChangesAsync"/> method that will apply all the pending changes.
/// </para>
/// <para>In the absence of this feature, it is assumed that lighting changes are applied immediately.</para>
/// </remarks>
public interface ILightingDeferredChangesFeature : ILightingDeviceFeature
{
	/// <summary>Applies changes to the current lighting effects.</summary>
	ValueTask ApplyChangesAsync();
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

/// <summary>A feature allowing to persist the applied lighting configuration on the device.</summary>
/// <remarks>Availability of this feature is not guaranteed.</remarks>
public interface IPersistentLightingFeature : ILightingDeviceFeature
{
	ValueTask PersistCurrentConfigurationAsync();
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
