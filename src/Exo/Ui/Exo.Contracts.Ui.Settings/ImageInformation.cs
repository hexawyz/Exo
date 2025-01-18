using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

/// <summary>Represents information on an image hosted in the scope of the service.</summary>
[DataContract]
public sealed class ImageInformation
{
	// NB: Image name is used as a public-facing unique key, but the images will internally use another index.
	[DataMember(Order = 1)]
	public required string ImageName { get; init; }
	[DataMember(Order = 2)]
	public required string FileName { get; init; }
	[DataMember(Order = 3)]
	public required ushort Width { get; init; }
	[DataMember(Order = 4)]
	public required ushort Height { get; init; }
	[DataMember(Order = 5)]
	public required ImageFormat Format { get; init; }
	[DataMember(Order = 6)]
	public bool IsAnimated { get; init; }
}
