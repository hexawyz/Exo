using System.Collections.Immutable;
using Exo.Lighting;

namespace Exo.Service;

public readonly struct LightingDeviceInformation
{
	public LightingZoneInformation? UnifiedLightingZone { get; }
	public ImmutableArray<LightingZoneInformation> LightingZones { get; }

	public LightingDeviceInformation(LightingZoneInformation? unifiedLightingZone, ImmutableArray<LightingZoneInformation> lightingZones)
	{
		UnifiedLightingZone = unifiedLightingZone;
		LightingZones = lightingZones;
	}
}

public readonly struct LightingDeviceWatchNotification
{
	public LightingDeviceWatchNotification(WatchNotificationKind kind, DeviceInformation driverInformation, LightingDeviceInformation lightingDeviceInformation, Driver? driver)
	{
		Kind = kind;
		DeviceInformation = driverInformation;
		LightingDeviceInformation = lightingDeviceInformation;
		Driver = driver;
	}

	/// <summary>Gets the kind of notification.</summary>
	public WatchNotificationKind Kind { get; }

	/// <summary>Gets the device information.</summary>
	/// <remarks>Because the driver instance may become invalid, useful information about the device and its driver is preserved here.</remarks>
	public DeviceInformation DeviceInformation { get; }

	/// <summary>Gets the lighting information.</summary>
	public LightingDeviceInformation LightingDeviceInformation { get; }

	/// <summary>Gets the actual driver instance.</summary>
	/// <remarks>
	/// This property is not assigned any value for a <see cref="WatchNotificationKind.Removal"/> notification.
	/// While the <see cref="Driver"/> instance may still be valid, and possibly somewhat useable, we consider it lost.
	/// The removal notification essentially serves for a way to notify consumers that they should remove their reference to the driver instance.
	/// </remarks>
	public Driver? Driver { get; }
}

/// <summary></summary>
/// <remarks>Effect notifications are always considered to be updates, even if the initial notifications will enumerate through all the zones.</remarks>
public readonly struct LightingEffectWatchNotification
{
	/// <summary>Gets the device information.</summary>
	public DeviceInformation DeviceInformation { get; }

	/// <summary>Gets the lighting zone information.</summary>
	public LightingZoneInformation ZoneInformation { get; }

	/// <summary>Gets the lighting zone whose current effect has changed.</summary>
	public ILightingZone Zone { get; }
}
