using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class ImageRegistrationEndRequest
{
	[DataMember(Order = 1)]
	public required Guid RequestId { get; init; }
}
