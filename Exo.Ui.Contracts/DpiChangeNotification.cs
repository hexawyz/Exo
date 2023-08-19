using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DpiChangeNotification
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }

	[DataMember(Order = 2)]
	public required DotsPerInch Dpi { get; init; }
}
