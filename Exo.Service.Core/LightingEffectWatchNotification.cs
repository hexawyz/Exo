using Exo.Contracts;

namespace Exo.Service;

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
	public LightingEffectWatchNotification(Guid deviceId, Guid zoneId, LightingEffect? serializedEffect)
	{
		DeviceId = deviceId;
		ZoneId = zoneId;
		_serializedEffect = serializedEffect;
	}

	/// <summary>Gets the ID of the device on which the effect was applied.</summary>
	public Guid DeviceId { get; }

	/// <summary>Gets the ID of the lighting zone on which the effect was applied.</summary>
	public Guid ZoneId { get; }

	private readonly LightingEffect? _serializedEffect;

	///// <summary>Gets the effect that was applied to the zone.</summary>
	//public ILightingEffect GetEffect() => null;

	/// <summary>Gets the effect that was applied to the zone as a serialized structure.</summary>
	public LightingEffect? SerializeEffect() => _serializedEffect;
}
