using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public class DeviceBrightnessLevel
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required byte BrightnessLevel { get; init; }
}
