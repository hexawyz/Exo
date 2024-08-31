using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class DeviceRequest
{
	[DataMember(Order = 1)]
	public required Guid Id { get; init; }
}
