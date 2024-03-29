using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MouseDeviceInformation
{
	[DataMember(Order = 1)]
	public required int ButtonCount { get; init; }
	[DataMember(Order = 2)]
	public required DotsPerInch MaximumDpi { get; init; }
	[DataMember(Order = 3)]
	public required bool HasSeparableDpi { get; init; }
}
