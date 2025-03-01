using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightInformation
{
	[DataMember(Order = 1)]
	public required Guid LightId { get; init; }
	[DataMember(Order = 2)]
	public LightCapabilities Capabilities { get; init; }
	[DataMember(Order = 3)]
	public byte MinimumBrightness { get; init; }
	[DataMember(Order = 4)]
	public byte MaximumBrightness { get; init; }
	[DataMember(Order = 5)]
	public uint MinimumTemperature { get; init; }
	[DataMember(Order = 6)]
	public uint MaximumTemperature { get; init; }
}
