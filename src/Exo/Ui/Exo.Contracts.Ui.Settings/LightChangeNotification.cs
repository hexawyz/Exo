using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightChangeNotification
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required Guid LightId { get; init; }
	[DataMember(Order = 3)]
	public required bool IsOn { get; init; }
	[DataMember(Order = 4)]
	public required byte Brightness { get; init; }
	[DataMember(Order = 5)]
	public required uint Temperature { get; init; }
}
