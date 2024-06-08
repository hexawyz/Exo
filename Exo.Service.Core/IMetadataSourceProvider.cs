namespace Exo.Service;

public interface IMetadataSourceProvider
{
	IAsyncEnumerable<MetadataSourceChangeNotification> WatchMetadataSourceChangesAsync(CancellationToken cancellationToken);
}
