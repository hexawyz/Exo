using System.Collections.Immutable;

namespace Exo.Monitors;

public readonly struct MonitorDefinition
{
	public string? Name { get; init; }
	public string? Capabilities { get; init; }
	public ImmutableArray<MonitorFeatureDefinition> OverriddenFeatures { get; init; }
	public ImmutableArray<byte> IgnoredCapabilitiesVcpCodes { get; init; }
	public bool IgnoreAllCapabilitiesVcpCodes { get; init; }
}

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

public readonly struct MonitorFeatureDiscreteValueDefinition
{
	public required ushort Value { get; init; }
	public Guid? NameStringId { get; init; }
}

public enum MonitorFeature : byte
{
	Other = 0,
	Brightness,
	Contrast,
	AudioVolume,
	InputSource,
}

public enum MonitorFeatureAccess : byte
{
	ReadWrite = 0,
	ReadOnly = 1,
	WriteOnly = 2,
}
