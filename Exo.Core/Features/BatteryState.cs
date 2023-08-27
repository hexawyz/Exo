namespace Exo.Features;

public record struct BatteryState
{
	public float? Level { get; init; }
	public BatteryStatus BatteryStatus { get; init; }
	public ExternalPowerStatus ExternalPowerStatus { get; init; }
}
