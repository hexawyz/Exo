using System.Collections.Immutable;

namespace Exo.Monitors;

public readonly struct MonitorFeatureDefinition
{
	public Guid? NameStringId { get; init; }
	public required byte VcpCode { get; init; }
	public MonitorFeatureAccess Access { get; init; }
	public required MonitorFeature Feature { get; init; }
	public ImmutableArray<MonitorFeatureDiscreteValueDefinition> DiscreteValues { get; init; }
	public ushort? MinimumValue { get; init; }
	public ushort? MaximumValue { get; init; }
}
