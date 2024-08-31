using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class PowerDeviceInformation
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required bool IsConnected { get; init; }
	[DataMember(Order = 3)]
	public required PowerDeviceCapabilities Capabilities { get; init; }
	[DataMember(Order = 4)]
	public required TimeSpan MinimumIdleTime { get; init; }
	[DataMember(Order = 5)]
	public required TimeSpan MaximumIdleTime { get; init; }
}
