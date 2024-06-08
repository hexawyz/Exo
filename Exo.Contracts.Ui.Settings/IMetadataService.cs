using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Metadata")]
public interface IMetadataService
{
	[OperationContract(Name = "GetMainStringsArchivePath")]
	ValueTask<string> GetMainStringsArchivePathAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchMetadataSourceChanges")]
	IAsyncEnumerable<MetadataSourceChangeNotification> WatchMetadataSourceChangesAsync(CancellationToken cancellationToken);
}
