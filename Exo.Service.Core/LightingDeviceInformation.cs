using System;
using System.Collections.Immutable;
using Exo.Lighting;
using Exo.Lighting.Effects;

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
/// <remarks>
/// <para>Effect notifications are always considered to be updates, even if the initial notifications will enumerate through all the zones.</para>
/// <para>
/// These notifications are kept as simple as possible in order to preserve efficiency when effects change frequently.
/// As such, this should contain exactly the same information as can be passed to <see cref="LightingService.SetEffect{TEffect}(Guid, Guid, in TEffect)" />.
/// Most clients of the effect notifications are assumed to already now about the available devices and lighting zones.
/// </para>
/// </remarks>
public readonly struct LightingEffectWatchNotification
{
	public LightingEffectWatchNotification(Guid deviceId, Guid zoneId, ILightingEffect effect)
	{
		DeviceId = deviceId;
		ZoneId = zoneId;
		Effect = effect;
	}

	/// <summary>Gets the ID of the device on which the effect was applied.</summary>
	public Guid DeviceId { get; }

	/// <summary>Gets the ID of the lighting zone on which the effect was applied.</summary>
	public Guid ZoneId { get; }

	/// <summary>Gets the effect that was applied to the zone.</summary>
	public ILightingEffect Effect { get; }
}
