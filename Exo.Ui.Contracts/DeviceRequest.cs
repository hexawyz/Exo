using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceRequest
{
	[DataMember(Order = 1)]
	public required Guid Id { get; init; }
}
