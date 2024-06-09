using IMetadataService = Exo.Metadata.IMetadataService;

namespace Exo.Settings.Ui.Services;

internal interface ISettingsMetadataService : IMetadataService
{
	Task WaitForAvailabilityAsync(CancellationToken cancellationToken);
}
