using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings.Cooling;

[DataContract]
public sealed class FixedCoolingParameters : ICoolingParameters
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required Guid CoolerId { get; init; }
	[DataMember(Order = 3)]
	public required byte Power { get; init; }
}
