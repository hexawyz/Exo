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

	/// <summary>Begins an image upload to the service by requesting a buffer for the specified image.</summary>
	/// <remarks>
	/// This method will asynchronously allocate a shared memory buffer and keep it open until the call is completed.
	/// As such, the stream will only return a single value, then the method will wait either cancellation or proper completion through a call to <see cref="EndAddImageAsync(ImageRegistrationEndRequest, CancellationToken)"/>.
	/// </remarks>
	/// <param name="request"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "BeginAddImage")]
	IAsyncEnumerable<ImageRegistrationBeginResponse> BeginAddImageAsync(ImageRegistrationBeginRequest request, CancellationToken cancellationToken);

	/// <summary>Completes an image upload after having written the data to the shared memory buffer.</summary>
	/// <param name="response"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "EndAddImage")]
	ValueTask EndAddImageAsync(ImageRegistrationEndRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "RemoveImage")]
	ValueTask RemoveImageAsync(ImageReference request, CancellationToken cancellationToken);
}
