using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Features.LightingFeatures;

/// <summary>Defines features related to lighting.</summary>
/// <remarks>
/// <para>Devices providing controllable lights control should expose features based on <see cref="ILightingDeviceFeature"/> to allow client components to control the lighting.</para>
/// <para>
/// Lighting capabilities can be very uneven across devices.
/// As such, we try to provide a realistic abstraction here so that drivers can expose their supported lighting modes in the most direct way possible.
/// </para>
/// <para>
/// The lighting model is built around the idea of lighting zones.
/// A lighting zone is a grouping of one or more lights that can be controlled by applying various effects at the same time.
/// Except for the <see cref="IUnifiedLightingFeature"/> that allows controlling a whole device as one single light zone,
/// drivers should expose light zones in a way that is as close as possible to the real hardware. i.e. The embedded lighting controller in the device.
/// </para>
/// <para>
/// In theory, any device could be generalized into an array of addressable RGB colors, mapping to a single light zone.
/// However, that would not be an appropriate representation for many devices, as some light effects can only be applied to certain physical zones on some devices.
/// Leveraging the intrinsic effects of the embedded RGB controllers is an important feature to have, as controlling lighting animations manually can be costly on the software side.
/// As such, the abstraction provided here intends to expose the features supported by the RGB controller with as much fidelity as possible.
/// More advanced features, such as controlling various light zones, even across multiple devices, in a synchronized way, can be left to other more generic components.
/// </para>
/// <para>
/// All devices should implement at least one of <see cref="ILightingControllerFeature"/> or <see cref="IUnifiedLightingFeature"/>.
/// While only one of those implementations should be used at the same time, most devices should implement both features.
/// It is important to note that some lighting controllers can benefit from more efficient global control of all lighting zones, and even provide specific effects when controlling all lights at once.
/// In these case, the <see cref="IUnifiedLightingFeature"/> is more than just a helpful shortcut to control device lighting.
/// </para>
/// <para>
/// All lighting device drivers should buffer effect changes and apply them only once <see cref="ILightingControllerFeature.ApplyChanges"/> (or the equivalent <see cref="IUnifiedLightingFeature"/>)
/// is called. This ensures that multiple lighting zones on the same device can be updated close to simultaneously, as efficiently as possible, and provides consistency between lighting mode updates.
/// </para>
/// <para>
/// Dynamic addressable lighting effects still need to be applied in the same way as other effects, but further updates to the colors must be processed by the effect itself, likely by the means of
/// a specific <c>ApplyChanges</c> or <c>Flush</c> method implemented on the effect.
/// </para>
/// </remarks>
public interface ILightingDeviceFeature : IDeviceFeature
{
}

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
	/// <summary>Applies changes to the current lighting effects.</summary>
	/// <remarks>
	/// This method is interchangeable with <see cref="IUnifiedLightingFeature.ApplyChanges"/>.
	/// Both interfaces should generally use the sample implementation for the method.
	/// They are duplicated in both interfaces because a device does not strictly need to implement both, although it is advised to do so when it makes sense.
	/// </remarks>
	void ApplyChanges();
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
	/// <summary>Applies changes to the current lighting effects.</summary>
	/// <remarks>
	/// This method is interchangeable with <see cref="ILightingControllerFeature.ApplyChanges"/>.
	/// Both interfaces should generally use the sample implementation for the method.
	/// They are duplicated in both interfaces because a device does not strictly need to implement both, although it is advised to do so when it makes sense.
	/// </remarks>
	void ApplyChanges();
}

/// <summary>A feature allowing to persist the applied lighting configuration on the device.</summary>
/// <remarks>Availability of this feature is not guaranteed.</remarks>
public interface IPersistentLightingFeature : ILightingDeviceFeature
{
	void PersistCurrentConfiguration();
}
