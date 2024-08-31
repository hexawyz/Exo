using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Metadata")]
public interface IMetadataService
{
	[OperationContract(Name = "GetMainStringArchivePathAsync")]
	ValueTask<string> GetMainStringArchivePathAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "GetMainLightingEffectArchivePathAsync")]
	ValueTask<string> GetMainLightingEffectArchivePathAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchMetadataSourceChanges")]
	IAsyncEnumerable<MetadataSourceChangeNotification> WatchMetadataSourceChangesAsync(CancellationToken cancellationToken);
}
