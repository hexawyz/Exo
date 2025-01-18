using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

/// <summary>This service manages the internal image collection.</summary>
/// <remarks>The image collection is a persisted collection of images to be used within the service.</remarks>
[ServiceContract(Name = "Images")]
public interface IImageService
{
	/// <summary>Watches information on images available in the service.</summary>
	/// <remarks>Images available are not necessarily loaded in memory.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchImages")]
	IAsyncEnumerable<WatchNotification<ImageInformation>> WatchImagesAsync(CancellationToken cancellationToken);

	// TODO: Maybe surface an error code so that the reason for a fail are more easily accessible.
	[OperationContract(Name = "AddImage")]
	ValueTask AddImageAsync(ImageRegistrationRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "RemoveImage")]
	ValueTask RemoveImageAsync(ImageReference request, CancellationToken cancellationToken);
}
