using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MouseActiveDpiPresetUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required byte ActivePresetIndex { get; init; }
}
