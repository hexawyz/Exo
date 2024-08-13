using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class DpiChangeNotification
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }

	[DataMember(Order = 2)]
	public required byte? PresetIndex { get; init; }

	[DataMember(Order = 3)]
	public required DotsPerInch Dpi { get; init; }
}
