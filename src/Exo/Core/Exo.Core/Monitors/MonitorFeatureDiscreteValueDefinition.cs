namespace Exo.Monitors;

public readonly struct MonitorFeatureDiscreteValueDefinition
{
	public required ushort Value { get; init; }
	public Guid? NameStringId { get; init; }
}
