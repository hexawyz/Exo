namespace Exo.Service;

public readonly struct MetadataSourceInformation
{
	public required MetadataArchiveCategory Category { get; init; }
	public required string ArchivePath { get; init; }
}
