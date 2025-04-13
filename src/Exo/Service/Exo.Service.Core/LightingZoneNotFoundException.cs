namespace Exo.Service;

public class LightingZoneNotFoundException : Exception
{
	public Guid ZoneId { get; }

	public LightingZoneNotFoundException() : this(default, $"Could not find the zone on the specified device.")
	{
	}

	public LightingZoneNotFoundException(Guid zoneId) : this(zoneId, $"Could not find the zone with ID {zoneId:B} on the specified device.")
	{
	}

	public LightingZoneNotFoundException(Guid zoneId, string? message) : base(message)
	{
		ZoneId = zoneId;
	}
}
