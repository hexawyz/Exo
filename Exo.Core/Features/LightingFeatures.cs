using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Features.LightingFeatures;

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
	/// <summary>Applies changes to the current lighting effects.</summary>
	/// <remarks>
	/// This method is interchangeable with <see cref="ILightingControllerFeature.ApplyChanges"/>.
	/// Both interfaces should generally use the sample implementation for the method.
	/// They are duplicated in both interfaces because a device does not strictly need to implement both, although it is advised to do so when it makes sense.
	/// </remarks>
	ValueTask ApplyChangesAsync();
}

/// <summary>A feature allowing to persist the applied lighting configuration on the device.</summary>
/// <remarks>Availability of this feature is not guaranteed.</remarks>
public interface IPersistentLightingFeature : ILightingDeviceFeature
{
	void PersistCurrentConfiguration();
}
