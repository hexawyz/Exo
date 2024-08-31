using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum CoolerType
{
	[EnumMember]
	Other = 0,
	[EnumMember]
	Fan = 1,
	[EnumMember]
	Pump = 2,
}
