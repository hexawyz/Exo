using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class ImageRegistrationRequest
{
	[DataMember(Order = 1)]
	public required string ImageName { get; init; }
	[DataMember(Order = 2)]
	public required string SharedMemoryName { get; init; }
	[DataMember(Order = 3)]
	public required ulong SharedMemoryLength { get; init; }
}
