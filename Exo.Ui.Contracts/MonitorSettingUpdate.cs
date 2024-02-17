using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class MonitorSettingUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required MonitorSetting Setting { get; init; }
	[DataMember(Order = 3)]
	public required ushort Value { get; init; }
}
