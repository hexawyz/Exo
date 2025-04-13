namespace Exo.Service;

public readonly struct MetadataSourceInformation(MetadataArchiveCategory category, string archivePath)
{
	public MetadataArchiveCategory Category { get; } = category;
	public string ArchivePath { get; } = archivePath;
}
