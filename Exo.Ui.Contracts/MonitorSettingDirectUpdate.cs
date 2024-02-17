using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class MonitorSettingDirectUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required ushort Value { get; init; }
}
