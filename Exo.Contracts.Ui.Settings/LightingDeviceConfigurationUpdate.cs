using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public class LightingDeviceConfigurationUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required bool IsUnifiedLightingEnabled { get; init; }
	[DataMember(Order = 3)]
	public required byte? BrightnessLevel { get; init; }
}
