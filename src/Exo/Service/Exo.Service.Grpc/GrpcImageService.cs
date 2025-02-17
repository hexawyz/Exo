using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Exo.Memory;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcImageService : IImageService
{
	private readonly ImageStorageService _imageStorageService;
	private readonly ILogger<GrpcImageService> _logger;
	private ImageAddState? _imageAddState;

	public GrpcImageService(ILogger<GrpcImageService> logger, ImageStorageService imagesService)
	{
		_logger = logger;
		_imageStorageService = imagesService;
	}

	public async IAsyncEnumerable<WatchNotification<Contracts.Ui.Settings.ImageInformation>> WatchImagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcImageServiceImageWatchStart();
		try
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
		finally
		{
			_logger.GrpcImageServiceImageWatchStop();
		}
	}

	public async IAsyncEnumerable<ImageRegistrationBeginResponse> BeginAddImageAsync(ImageRegistrationBeginRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcImageServiceAddImageRequestStart(request.ImageName, request.Length);

		ImageAddState state;

		try
		{
			if (await _imageStorageService.HasImageAsync(request.ImageName, cancellationToken).ConfigureAwait(false))
			{
				throw new RpcException(new(StatusCode.InvalidArgument, "An image with the specified name is already present."));
			}
			// 24MB is the maximum image size that could be theoretically allocated on the NZXT Kraken Z, so it seems reasonable to fix this as the limit for now?
			// This number is semi-arbitrary, as we have to allow somewhat large images, but we don't want to allow anything crazy.
			// The risk is someone abusing the service to waste memory. Stupid but possible.
			// We have to be careful because apps cannot create shared memory in the Global namespace themselves, so the service needs to handle it ☹️
			// This is also the reason why only a single parallel request is allowed. Anyway, this would never be a problem in practice as the UI is supposed to be the unique client.
			if (request.Length > 24 * 1024 * 1024) throw new RpcException(new(StatusCode.InvalidArgument, "Maximum allowed image size exceeded."));

			state = new ImageAddState(request.ImageName);

			if (Interlocked.CompareExchange(ref _imageAddState, state, null) is not null)
			{
				state.Dispose();
				throw new RpcException(new(StatusCode.FailedPrecondition, "Another operation is currently pending."));
			}
		}
		catch (Exception ex)
		{
			_logger.GrpcImageServiceAddImageRequestFailure(request.ImageName, ex);
			throw;
		}

		try
		{
			yield return new() { RequestId = state.RequestId, SharedMemoryName = state.Initialize(request.Length) };
			try
			{
				await state.WaitForWriteCompletion().WaitAsync(cancellationToken).ConfigureAwait(false);
				_logger.GrpcImageServiceAddImageRequestWriteComplete(request.ImageName);
				try
				{
					using (var memoryManager = state.CreateMemoryManagerForRead())
					{
						try
						{
							await _imageStorageService.AddImageAsync(request.ImageName, memoryManager.Memory, cancellationToken).ConfigureAwait(false);
						}
						catch (ArgumentException ex)
						{
							throw AsRpcException(ex);
						}
					}
					_logger.GrpcImageServiceAddImageRequestSuccess(request.ImageName);
					state.TryNotifyReadCompletion();
				}
				catch (Exception ex)
				{
					state.TryNotifyReadException(ex);
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.GrpcImageServiceAddImageRequestFailure(request.ImageName, ex);
				throw;
			}
		}
		finally
		{
			state.Dispose();
			Interlocked.CompareExchange(ref _imageAddState, null, state);
		}
	}

	public async ValueTask EndAddImageAsync(ImageRegistrationEndRequest request, CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _imageAddState) is not { } state || state.RequestId != request.RequestId || !state.TryNotifyWriteCompletion())
		{
			throw new RpcException(new(StatusCode.FailedPrecondition, "This operation cannot be performed."));
		}

		await state.WaitForReadCompletion().WaitAsync(cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask RemoveImageAsync(ImageReference request, CancellationToken cancellationToken)
	{
		try
		{
			await _imageStorageService.RemoveImageAsync(request.ImageName, cancellationToken).ConfigureAwait(false);
		}
		catch (ArgumentException ex)
		{
			throw AsRpcException(ex);
		}
	}

	private static RpcException AsRpcException(ArgumentException ex) => new RpcException(new(StatusCode.InvalidArgument, ex.Message));

	private sealed class ImageAddState : IDisposable
	{
		private readonly Guid _requestId;
		private readonly string _imageName;
		private TaskCompletionSource<bool>? _writeTaskCompletionSource;
		private TaskCompletionSource<bool>? _readTaskCompletionSource;
		private SharedMemory? _sharedMemory;

		public ImageAddState(string imageName)
		{
			_requestId = Guid.NewGuid();
			_imageName = imageName;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _writeTaskCompletionSource, null) is { } tcs1 && !tcs1.Task.IsCompleted) tcs1.TrySetResult(false);
			if (Interlocked.Exchange(ref _readTaskCompletionSource, null) is { } tcs2 && !tcs2.Task.IsCompleted) tcs2.TrySetResult(false);
			if (Interlocked.Exchange(ref _sharedMemory, null) is { } sharedMemory) sharedMemory.Dispose();
		}

		public Guid RequestId => _requestId;

		public MemoryMappedFileMemoryManager CreateMemoryManagerForRead() => (_sharedMemory ?? throw new InvalidOperationException()).CreateMemoryManager(MemoryMappedFileAccess.Read);

		public string Initialize(uint length)
		{
			_writeTaskCompletionSource = new();
			_sharedMemory = SharedMemory.Create("Exo_Image_", length);
			return _sharedMemory.Name;
		}

		public bool TryNotifyWriteCompletion() => _writeTaskCompletionSource?.TrySetResult(true) ?? false;
		public bool TryNotifyReadCompletion() => _readTaskCompletionSource?.TrySetResult(true) ?? false;
		public bool TryNotifyReadException(Exception ex) => _readTaskCompletionSource?.TrySetException(ex) ?? false;

		public Task<bool> WaitForWriteCompletion() => _writeTaskCompletionSource?.Task ?? Task.FromResult(false);
		public Task<bool> WaitForReadCompletion() => _readTaskCompletionSource?.Task ?? Task.FromResult(true);
	}
}
