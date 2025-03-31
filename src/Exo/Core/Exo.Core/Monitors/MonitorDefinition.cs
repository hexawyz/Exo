using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Exo.Monitors;

public readonly struct MonitorDefinition
{
	public string? Name { get; init; }
	[JsonConverter(typeof(Utf8StringConverter))]
	public ImmutableArray<byte> Capabilities { get; init; }
	public ImmutableArray<MonitorFeatureDefinition> OverriddenFeatures { get; init; }
	public ImmutableArray<byte> IgnoredCapabilitiesVcpCodes { get; init; }
	public bool IgnoreAllCapabilitiesVcpCodes { get; init; }
}
