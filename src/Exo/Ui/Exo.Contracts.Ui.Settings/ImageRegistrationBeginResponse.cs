using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class ImageRegistrationBeginResponse
{
	[DataMember(Order = 1)]
	public required Guid RequestId { get; init; }
	[DataMember(Order = 2)]
	public required string SharedMemoryName { get; init; }
}
