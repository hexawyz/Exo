using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly struct MetadataSourceInformation
{
	[DataMember(Order = 1)]
	public required MetadataArchiveCategory Category { get; init; }

	[DataMember(Order = 2)]
	public required string ArchivePath { get; init; }
}
