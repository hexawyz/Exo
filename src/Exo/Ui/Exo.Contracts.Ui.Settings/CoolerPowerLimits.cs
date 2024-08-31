using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class CoolerPowerLimits
{
	[DataMember(Order = 1)]
	public byte MinimumPower { get; init; }
	[DataMember(Order = 2)]
	public bool CanSwitchOff { get; init; }
}
