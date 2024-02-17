using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class MonitorSettingValue
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }

	[DataMember(Order = 2)]
	public required MonitorSetting Setting { get; init; }

	[DataMember(Order = 3)]
	public required ushort CurrentValue { get; init; }

	[DataMember(Order = 4)]
	public required ushort MinimumValue { get; init; }

	[DataMember(Order = 5)]
	public required ushort MaximumValue { get; init; }
}
