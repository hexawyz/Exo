using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Features;
using Exo.Programming;

namespace Exo.Service.Events;

[DataContract]
[TypeId(0x80B7ACBE, 0x8D96, 0x418B, 0x9F, 0x23, 0x77, 0x8B, 0x81, 0xA9, 0x30, 0xEC)]
public class BatteryEventParameters : DeviceEventParameters
{
	public BatteryEventParameters(DeviceId deviceId, float? currentLevel, float? previousLevel, BatteryStatus status, ExternalPowerStatus externalPowerStatus) : base(deviceId)
	{
		CurrentLevel = currentLevel;
		PreviousLevel = previousLevel;
		Status = status;
		ExternalPowerStatus = externalPowerStatus;
	}

	[DataMember(Order = 2)]
	[Description("The current battery level, expressed from 0 to 1.")]
	public float? CurrentLevel { get; }
	[DataMember(Order = 3)]
	[Description("The previous battery level, expressed from 0 to 1.")]
	public float? PreviousLevel { get; }
	[DataMember(Order = 4)]
	[Description("The battery status.")]
	public BatteryStatus Status { get; }
	[DataMember(Order = 5)]
	[Description("Indicates the status of external power connection.")]
	public ExternalPowerStatus ExternalPowerStatus { get; }
}
