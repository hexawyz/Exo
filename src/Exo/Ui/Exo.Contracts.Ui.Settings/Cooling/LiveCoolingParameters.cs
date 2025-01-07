using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings.Cooling;

/// <summary>Represents the live cooling power of a cooler.</summary>
/// <remarks>This is not a cooling mode, but a message sent to notify the UI of a dynamic change in cooling power, when a control curve is applied.</remarks>
[DataContract]
public sealed class LiveCoolingParameters : ICoolingParameters
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required Guid CoolerId { get; init; }
	[DataMember(Order = 3)]
	public required byte Power { get; init; }
}
