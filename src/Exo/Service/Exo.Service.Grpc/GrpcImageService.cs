using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;

namespace Exo.Service.Grpc;

internal sealed class GrpcImageService : IImageService
{
	private readonly ImageStorageService _imageStorageService;

	public GrpcImageService(ImageStorageService imagesService) => _imageStorageService = imagesService;

	public async IAsyncEnumerable<WatchNotification<Contracts.Ui.Settings.ImageInformation>> WatchImagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _imageStorageService.WatchChangesAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return new()
			{
				NotificationKind = notification.Kind.ToGrpc(),
				Details = notification.ImageInformation.ToGrpc(),
			};
		}
	}

	public async ValueTask AddImageAsync(ImageRegistrationRequest request, CancellationToken cancellationToken)
	{
		await _imageStorageService.AddImageAsync(request.ImageName, request.Data, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask RemoveImageAsync(ImageReference request, CancellationToken cancellationToken)
	{
		await _imageStorageService.RemoveImageAsync(request.ImageName, cancellationToken).ConfigureAwait(false);
	}
}
