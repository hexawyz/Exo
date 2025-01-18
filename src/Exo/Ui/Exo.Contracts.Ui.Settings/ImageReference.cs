using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class ImageReference
{
	[DataMember(Order = 1)]
	public required string ImageName { get; init; }
}
