using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class ImageRegistrationBeginRequest
{
	[DataMember(Order = 1)]
	public required string ImageName { get; init; }
	[DataMember(Order = 2)]
	public required uint Length { get; init; }
}
