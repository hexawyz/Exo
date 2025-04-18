namespace Exo.Settings.Ui.Services;

// NB: For safety reasons, the service can only process a single image add operation at a time.
// Adding images concurrently is a scenario that should never need to be supported anyway. So we won't.
internal interface IImageService
{
	Task<string> BeginAddImageAsync(string imageName, uint length, CancellationToken cancellationToken);
	Task CancelAddImageAsync(string sharedMemoryName, CancellationToken cancellationToken);
	Task EndAddImageAsync(string sharedMemoryName, CancellationToken cancellationToken);
	Task RemoveImageAsync(UInt128 imageId, CancellationToken cancellationToken);
}
