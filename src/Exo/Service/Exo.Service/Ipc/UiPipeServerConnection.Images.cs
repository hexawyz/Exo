using System.IO.MemoryMappedFiles;
using Exo.Memory;
using Exo.Primitives;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchImagesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<ImageChangeNotification>.CreateAsync(_server.ImageStorageService, cancellationToken))
		{
			try
			{
				await WriteInitialDataAsync(watcher, cancellationToken).ConfigureAwait(false);
				await WriteConsumedDataAsync(watcher, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<ImageChangeNotification> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var notification in initialData)
					{
						int length = WriteNotification(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<ImageChangeNotification> watcher, CancellationToken cancellationToken)
		{
			while (await watcher.Reader.WaitToReadAsync().ConfigureAwait(false) && !cancellationToken.IsCancellationRequested)
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var notification))
					{
						int length = WriteNotification(buffer.Span, notification);
						if (cancellationToken.IsCancellationRequested) return;
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteNotification(Span<byte> buffer, in ImageChangeNotification notification)
		{
			var writer = new BufferWriter(buffer);
			var message = notification.Kind switch
			{
				WatchNotificationKind.Enumeration => ExoUiProtocolServerMessage.ImageEnumeration,
				WatchNotificationKind.Addition => ExoUiProtocolServerMessage.ImageAdd,
				WatchNotificationKind.Removal => ExoUiProtocolServerMessage.ImageRemove,
				WatchNotificationKind.Update => ExoUiProtocolServerMessage.ImageUpdate,
				_ => throw new InvalidOperationException(),
			};
			writer.Write((byte)message);
			Write(ref writer, notification.ImageInformation);
			return (int)writer.Length;
		}
	}

	// For simplicity, the image operations are handled "synchronously" in regards to the connection.
	// It is totally possible to make this not block the flow of incoming requests, but there is no realistic scenario where blocking here would be a problem.
	// Blocking won't be for long anyway.
	private ValueTask<bool> ProcessImageAddBeginAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		string? imageName = reader.ReadVariableString();
		uint length = reader.Read<uint>();
		return ProcessImageAddBeginAsync(imageName, length, cancellationToken);
	}

	private async ValueTask<bool> ProcessImageAddBeginAsync(string? imageName, uint length, CancellationToken cancellationToken)
	{
		if (imageName is null)
		{
			await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.InvalidArgument, null, cancellationToken).ConfigureAwait(false);
			return true;
		}
		if (await _server.ImageStorageService.HasImageAsync(imageName, cancellationToken).ConfigureAwait(false))
		{
			await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.NameAlreadyInUse, null, cancellationToken).ConfigureAwait(false);
			return true;
		}
		if (_imageUploadSharedMemory is not null)
		{
			await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.ConcurrentOperation, null, cancellationToken).ConfigureAwait(false);
			return true;
		}
		_imageUploadSharedMemory = SharedMemory.Create("Exo_Image_", length);
		_imageUploadImageName = imageName;
		await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.Success, _imageUploadSharedMemory.Name, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private ValueTask<bool> ProcessImageAddCancelAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		string? sharedMemoryName = reader.ReadVariableString();
		return ProcessImageAddCancelAsync(sharedMemoryName, cancellationToken);
	}

	private async ValueTask<bool> ProcessImageAddCancelAsync(string? sharedMemoryName, CancellationToken cancellationToken)
	{
		if (sharedMemoryName is null || _imageUploadImageName is null || _imageUploadSharedMemory is null || _imageUploadSharedMemory.Name != sharedMemoryName)
		{
			await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.InvalidArgument, sharedMemoryName, cancellationToken).ConfigureAwait(false);
			return true;
		}
		_imageUploadImageName = null;
		Interlocked.Exchange(ref _imageUploadSharedMemory, null).Dispose();
		await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.Success, sharedMemoryName, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private ValueTask<bool> ProcessImageAddEndAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		string? sharedMemoryName = reader.ReadVariableString();
		return ProcessImageAddEndAsync(sharedMemoryName, cancellationToken);
	}

	private async ValueTask<bool> ProcessImageAddEndAsync(string? sharedMemoryName, CancellationToken cancellationToken)
	{
		if (sharedMemoryName is null || _imageUploadImageName is null || _imageUploadSharedMemory is null || _imageUploadSharedMemory.Name != sharedMemoryName)
		{
			await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.InvalidArgument, sharedMemoryName, cancellationToken).ConfigureAwait(false);
			return true;
		}
		using (var memoryManager = _imageUploadSharedMemory.CreateMemoryManager(MemoryMappedFileAccess.Read))
		{
			try
			{
				await _server.ImageStorageService.AddImageAsync(_imageUploadImageName!, memoryManager.Memory, cancellationToken).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.InvalidArgument, sharedMemoryName, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (Exception)
			{
				await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.Error, sharedMemoryName, cancellationToken).ConfigureAwait(false);
				return true;
			}
		}
		Interlocked.Exchange(ref _imageUploadSharedMemory, null).Dispose();
		await WriteImageStorageAddStatusAsync(ImageStorageOperationStatus.Success, sharedMemoryName, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private ValueTask<bool> ProcessImageRemoveAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		var imageId = reader.Read<UInt128>();
		return ProcessImageRemoveAsync(imageId, cancellationToken);
	}

	private async ValueTask<bool> ProcessImageRemoveAsync(UInt128 imageId, CancellationToken cancellationToken)
	{
		try
		{
			await _server.ImageStorageService.RemoveImageAsync(imageId, cancellationToken).ConfigureAwait(false);
		}
		catch (ImageNotFoundException)
		{
			await WriteImageStorageRemoveStatusAsync(ImageStorageOperationStatus.ImageNotFound, imageId, cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (Exception)
		{
			await WriteImageStorageRemoveStatusAsync(ImageStorageOperationStatus.Error, imageId, cancellationToken).ConfigureAwait(false);
			return true;
		}
		await WriteImageStorageRemoveStatusAsync(ImageStorageOperationStatus.Success, imageId, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private async ValueTask WriteImageStorageAddStatusAsync(ImageStorageOperationStatus status, string? name, CancellationToken cancellationToken)
	{
		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = WriteBuffer;
			nuint length = Write(buffer.Span, status, name);
			await WriteAsync(buffer[..(int)length], cancellationToken).ConfigureAwait(false);
		}

		static nuint Write(Span<byte> buffer, ImageStorageOperationStatus status, string? name)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.ImageAddOperationStatus);
			writer.Write((byte)status);
			writer.WriteVariableString(name);
			return writer.Length;
		}
	}

	private async ValueTask WriteImageStorageRemoveStatusAsync(ImageStorageOperationStatus status, UInt128 imageId, CancellationToken cancellationToken)
	{
		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = WriteBuffer;
			nuint length = Write(buffer.Span, status, imageId);
			await WriteAsync(buffer[..(int)length], cancellationToken).ConfigureAwait(false);
		}

		static nuint Write(Span<byte> buffer, ImageStorageOperationStatus status, UInt128 imageId)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.ImageRemoveOperationStatus);
			writer.Write((byte)status);
			writer.Write(imageId);
			return writer.Length;
		}
	}

	private static void Write(ref BufferWriter writer, in ImageInformation information)
	{
		writer.Write(information.ImageId);
		writer.WriteVariableString(information.ImageName);
		writer.WriteVariableString(information.FileName);
		writer.Write(information.Width);
		writer.Write(information.Height);
		writer.Write((byte)information.Format);
		writer.Write(information.IsAnimated);
	}
}
