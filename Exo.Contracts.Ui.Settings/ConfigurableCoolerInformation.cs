using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class ConfigurableCoolerInformation
{
	[DataMember(Order = 1)]
	public byte MinimumValue { get; }
	[DataMember(Order = 2)]
	public bool CanSwitchOff { get; }
}
