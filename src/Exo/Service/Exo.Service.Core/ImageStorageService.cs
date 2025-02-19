using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Hashing;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Images;
using Exo.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Exo.Service;

internal sealed class ImageStorageService : IAsyncDisposable
{
	[TypeId(0x1D185C1A, 0x4903, 0x4D4A, 0x91, 0x20, 0x69, 0x4A, 0xE5, 0x2C, 0x07, 0x7A)]
	[method: JsonConstructor]
	private readonly struct ImageMetadata(UInt128 id, ushort width, ushort height, ImageFormat format, bool isAnimated)
	{
		public UInt128 Id { get; } = id;
		public ushort Width { get; } = width;
		public ushort Height { get; } = height;
		public ImageFormat Format { get; } = format;
		public bool IsAnimated { get; } = isAnimated;
	}

	private sealed class LiveImageMetadata(UInt128 id, string? imageName, ushort width, ushort height, ImageFormat format, bool isAnimated)
	{
		public UInt128 Id { get; } = id;
		public string? ImageName { get; } = imageName;
		public ushort Width { get; } = width;
		public ushort Height { get; } = height;
		public ImageFormat Format { get; } = format;
		public bool IsAnimated { get; } = isAnimated;

		public ImageMetadata ToMetadata() => new(Id, Width, Height, Format, IsAnimated);
	}

	private class SharedImageFile : ImageFile
	{
		private long _referenceCount;
		private readonly SharedImageSlot _slot;

		public SharedImageFile(SharedImageSlot slot, SafeFileHandle handle)
			: base(handle, MemoryMappedFileAccess.Read)
		{
			_referenceCount = 1;
			_slot = slot;
		}

		~SharedImageFile() => _slot?.TryFree(this);

		public override void Dispose()
		{
			if (Interlocked.Decrement(ref _referenceCount) == 0)
			{
				_slot.TryFree(this);
			}
		}

		public bool TryAcquire()
		{
			long refCount = Interlocked.Increment(ref _referenceCount);
			return refCount > 0;
		}

		public bool TryDispose()
		{
			if (Interlocked.CompareExchange(ref _referenceCount, long.MinValue >> 1, 0) == 0)
			{
				base.Dispose();
				return true;
			}
			return false;
		}
	}

	private sealed class SharedImageSlot
	{
		private readonly ImageStorageService _imageService;
		private readonly UInt128 _imageId;
		private GCHandle _gcHandle;
		private readonly string _fileName;

		public SharedImageSlot(ImageStorageService imageService, UInt128 imageId, string fileName)
		{
			_imageService = imageService;
			_imageId = imageId;
			_fileName = fileName;
			_gcHandle = GCHandle.Alloc(null, GCHandleType.Weak);
		}

		~SharedImageSlot() => _gcHandle.Free();

		public SharedImageFile? TryGetImage()
		{
			// The easy path is GC Handle allocated and a valid target that is not yet disposed.
			if (_gcHandle.IsAllocated)
			{
				if (_gcHandle.Target is { } target)
				{
					if (Unsafe.As<SharedImageFile>(target).TryAcquire())
					{
						return Unsafe.As<SharedImageFile>(target);
					}
				}

				lock (this)
				{
					// If the slot has been freed, the caller will have to try its luck again.
					if (_gcHandle.IsAllocated)
					{
						// Look up the easy path again in case another thread requested the image and it was allocated.
						if ((target = _gcHandle.Target) is not null)
						{
							if (Unsafe.As<SharedImageFile>(target).TryAcquire())
							{
								return Unsafe.As<SharedImageFile>(target);
							}
						}

						try
						{
							target = new SharedImageFile(this, File.OpenHandle(_fileName));
						}
						catch (IOException)
						{
							_imageService._openImageFiles.TryRemove(new(_imageId, this));
							_gcHandle.Free();
							GC.SuppressFinalize(this);
							throw;
						}
						_gcHandle.Target = target;
						return Unsafe.As<SharedImageFile>(target);
					}
				}
			}

			return null;
		}

		public void TryFree(SharedImageFile image)
		{
			lock (this)
			{
				if (image.TryDispose())
				{
					_gcHandle.Target = null;
				}
				// Allow other threads to potentially reuse the object before we unregister it.
				Monitor.Pulse(this);
				// If we confirm the object as not having been reused, we can clean everything up.
				if (_gcHandle.IsAllocated && _gcHandle.Target is null)
				{
					_imageService._openImageFiles.TryRemove(new(_imageId, this));
					_gcHandle.Free();
					GC.SuppressFinalize(this);
				}
			}
		}
	}

	private static string GetFileName(string imageCacheDirectory, UInt128 imageId) => Path.Combine(imageCacheDirectory, imageId.ToString("x32", CultureInfo.InvariantCulture));

	// Logical image metadata will be persisted in a single file in order to save disk space (more like a real database).
	// Considering that the number of images could grow organically (in scenarios where images are used at all),
	// storing metadata in individual files would waste storage for each image.
	// We consider the data to not be deletable in general, so what will happen is that barring the initial load,
	// metadata will be written to the file in append only mode.
	// During initial load, we will allow rewriting it if some entries are outdated.
	private static string GetLogicalImageCacheFileName(string imageCacheDirectory) => Path.Combine(imageCacheDirectory, "meta");

	private static string GetTemporaryFileName(string imageCacheDirectory)
		=> Path.Combine(imageCacheDirectory, "tmp", RandomNumberGenerator.GetHexString(32));

	public static async Task<ImageStorageService> CreateAsync
	(
		ILogger<ImageStorageService> logger,
		IConfigurationContainer<string> imagesConfigurationContainer,
		string imageCacheDirectory,
		CancellationToken cancellationToken
	)
	{
		if (!Path.IsPathRooted(imageCacheDirectory)) throw new ArgumentException("Images directory path must be rooted.");

		imageCacheDirectory = Path.GetFullPath(imageCacheDirectory);
		Directory.CreateDirectory(imageCacheDirectory);
		Directory.CreateDirectory(Path.Combine(imageCacheDirectory, "tmp"));

		var imageNames = await imagesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		var imageCollection = new Dictionary<string, LiveImageMetadata>(StringComparer.OrdinalIgnoreCase);
		var imageCollectionById = new ConcurrentDictionary<UInt128, LiveImageMetadata>();

		foreach (var imageName in imageNames)
		{
			var result = await imagesConfigurationContainer.ReadValueAsync<ImageMetadata>(imageName, cancellationToken).ConfigureAwait(false);
			if (result.Found)
			{
				if (!File.Exists(GetFileName(imageCacheDirectory, result.Value.Id)))
				{
					// TODO: Log warning about missing image being removed from the collection.
					await imagesConfigurationContainer.DeleteValueAsync<ImageMetadata>(imageName).ConfigureAwait(false);
					continue;
				}
				var metadata = result.Value;
				var liveMetadata = new LiveImageMetadata(metadata.Id, imageName, metadata.Width, metadata.Height, metadata.Format, metadata.IsAnimated);
				imageCollection.Add(imageName, liveMetadata);
				if (!imageCollectionById.TryAdd(metadata.Id, liveMetadata)) throw new InvalidOperationException("Duplicate image data.");
			}
		}

		var logicalImageMetadata = new ConcurrentDictionary<UInt128, ImageMetadata>();
		var logicalImageMetadataFile = new FileInfo(GetLogicalImageCacheFileName(imageCacheDirectory));
		bool shouldRewriteMetadata = false;
		try
		{
			using var logicalImageMetadataReadStream = logicalImageMetadataFile.Open
			(
				new FileStreamOptions()
				{
					Mode = FileMode.Open,
					Access = FileAccess.Read,
					Share = FileShare.Read,
					Options = FileOptions.SequentialScan | FileOptions.Asynchronous
				}
			);

			await foreach (var kvp in JsonSerializer.DeserializeAsyncEnumerable<KeyValuePair<UInt128, ImageMetadata>>(logicalImageMetadataReadStream, true, ConfigurationService.JsonSerializerOptions, cancellationToken))
			{
				if (!File.Exists(GetFileName(imageCacheDirectory, kvp.Value.Id)))
				{
					shouldRewriteMetadata = true;
				}
				else if (!logicalImageMetadata.TryAdd(kvp.Key, kvp.Value))
				{
					shouldRewriteMetadata = true;
					logicalImageMetadata[kvp.Key] = kvp.Value;
				}
			}
		}
		catch (FileNotFoundException)
		{
		}

		// We cannot open the file with FileMode.Truncate simultaneously with FileSystemRights.AppendData, so we do the recreation of the file in a separate path.
		if (shouldRewriteMetadata)
		{
			using var logicalImageMetadataRewriteStream = logicalImageMetadataFile.Open(
				new FileStreamOptions()
				{
					Mode = FileMode.Truncate,
					Access = FileAccess.Write,
					Share = FileShare.None,
					Options = FileOptions.SequentialScan | FileOptions.Asynchronous
				}
			);
			foreach (var kvp in logicalImageMetadata)
			{
				// We should not allow this call to be cancelled, as it would wipe out the entire cache.
				// Simultaneously, it might also be acceptable to wipe the cache. To revisit later.
				await JsonSerializer.SerializeAsync(logicalImageMetadataRewriteStream, kvp, ConfigurationService.JsonSerializerOptions, default).ConfigureAwait(false);
				await logicalImageMetadataRewriteStream.FlushAsync().ConfigureAwait(false);
			}
		}

		// We specify a small buffer size that should be able to hold any entry entirely.
		// This way, entries can be written atomically.
		// NB: From what I know, we still want to go through OS and hardware caches. What's most important is that the data is handed down to the OS as soon as possible.
		var logicalImageMetadataWriteStream = logicalImageMetadataFile.Create
		(
			FileMode.OpenOrCreate,
			FileSystemRights.AppendData,
			FileShare.Read,
			128,
			FileOptions.Asynchronous,
			null
		);

		return new(logger, imagesConfigurationContainer, imageCacheDirectory, imageCollection, imageCollectionById, logicalImageMetadata, logicalImageMetadataWriteStream);
	}

	private const long PhysicalImageIdHashSeed = unchecked((long)0x90AB71E534FD62C8U);
	private const long LogicalImageIdHashSeed = unchecked((long)0x548A41D831245BCAU);

	private readonly Dictionary<string, LiveImageMetadata> _imageCollection;
	private readonly ConcurrentDictionary<UInt128, LiveImageMetadata> _imageCollectionById;
	private readonly ConcurrentDictionary<UInt128, SharedImageSlot> _openImageFiles;
	private readonly ConcurrentDictionary<UInt128, ImageMetadata> _logicalImageMetadata;
	private readonly IConfigurationContainer<string> _imagesConfigurationContainer;
	private readonly string _imageCacheDirectory;
	private readonly ChannelWriter<KeyValuePair<UInt128, ImageMetadata>> _logicalImageMetadataPersistenceWriter;
	private ChannelWriter<ImageChangeNotification>[]? _changeListeners;
	private readonly AsyncLock _lock;
	private readonly ILogger<ImageStorageService> _logger;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _logicalImageMetadataPersistenceTask;

	private ImageStorageService
	(
		ILogger<ImageStorageService> logger,
		IConfigurationContainer<string> imagesConfigurationContainer,
		string imageCacheDirectory,
		Dictionary<string, LiveImageMetadata> imageCollection,
		ConcurrentDictionary<UInt128, LiveImageMetadata> imageCollectionById,
		ConcurrentDictionary<UInt128, ImageMetadata> logicalImageMappings,
		FileStream logicalImageMetadataPersistenceStream
	)
	{
		_logger = logger;
		_imagesConfigurationContainer = imagesConfigurationContainer;
		_imageCacheDirectory = imageCacheDirectory;
		_imageCollection = imageCollection;
		_imageCollectionById = imageCollectionById;
		_openImageFiles = new();
		_logicalImageMetadata = logicalImageMappings;
		// The channel used for persisting logical image metadata should be as responsive as possible.
		// We just want to avoid waiting for the FlushAsync call, but we want the largest part of the write to be done on the calling thread *when possible*.
		var logicalImageMetadataPersistenceChannel = Channel.CreateUnbounded<KeyValuePair<UInt128, ImageMetadata>>
		(
			new UnboundedChannelOptions()
			{
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = true
			}
		);
		_logicalImageMetadataPersistenceWriter = logicalImageMetadataPersistenceChannel;
		_lock = new();
		_cancellationTokenSource = new();
		_logicalImageMetadataPersistenceTask = PersistLogicalImageMetadataAsync(logicalImageMetadataPersistenceChannel, logicalImageMetadataPersistenceStream, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _logicalImageMetadataPersistenceTask.ConfigureAwait(false);
			_logicalImageMetadataPersistenceWriter.TryComplete();
			cts.Dispose();
		}
	}

	private async Task PersistLogicalImageMetadataAsync(ChannelReader<KeyValuePair<UInt128, ImageMetadata>> reader, FileStream logicalImageMetadataPersistenceStream, CancellationToken cancellationToken)
	{
		using (logicalImageMetadataPersistenceStream)
		{
			try
			{
				await foreach (var kvp in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
				{
					await JsonSerializer.SerializeAsync(logicalImageMetadataPersistenceStream, kvp, ConfigurationService.JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
					await logicalImageMetadataPersistenceStream.FlushAsync(cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
			}
		}
	}

	public async IAsyncEnumerable<ImageChangeNotification> WatchChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<ImageChangeNotification>();

		ImageInformation[]? images;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			images = new ImageInformation[_imageCollection.Count];
			int i = 0;
			foreach (var (name, metadata) in _imageCollection)
			{
				images[i++] = new(metadata.Id, name, GetFileName(_imageCacheDirectory, metadata.Id), metadata.Width, metadata.Height, metadata.Format, metadata.IsAnimated);
			}
			ArrayExtensions.InterlockedAdd(ref _changeListeners, channel);
		}

		try
		{
			foreach (var image in images)
			{
				yield return new(WatchNotificationKind.Enumeration, image);
			}
			images = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _changeListeners, channel);
		}
	}

	public ImageFile GetImageFile(UInt128 imageId)
	{
		while (true)
		{
			var slot = _openImageFiles.GetOrAdd
			(
				imageId,
				static (imageId, service) =>
				{
					string fileName = GetFileName(service._imageCacheDirectory, imageId);
					if (!File.Exists(fileName)) throw new FileNotFoundException(null, fileName);
					return new(service, imageId, fileName);
				},
				this
			);
			if (slot.TryGetImage() is { } image)
			{
				return image;
			}
		}
	}

	// TODO: This is imperfect. Make it better. Worst case, just acquire a lock at all times and simplify the code :(
	// Current problems: Computing the filename multiple times, no ability to wait for the file being written to disk.
	// Possibly, an optional AsyncLock could be pushed into the created Slot to allow for waiting.
	private ImageFile? TryGetImageFile(UInt128 imageId)
	{
		while (true)
		{
			if (!_openImageFiles.TryGetValue(imageId, out var slot))
			{
				string fileName = GetFileName(_imageCacheDirectory, imageId);
				if (!File.Exists(fileName)) return null;
				return GetImageFile(imageId);
			}
			if (slot.TryGetImage() is { } image)
			{
				return image;
			}
		}
	}

	// These images are intended to be sent straight to devices in their binary encoded format.
	public (UInt128 ImageId, ImageFormat Format, ImageFile Image) GetTransformedImage
	(
		UInt128 imageId,
		Rectangle sourceRectangle,
		ImageFormats targetStaticFormats,
		ImageFormats targetAnimatedFormats,
		PixelFormat targetPixelFormat,
		Size targetSize,
		ImageRotation targetRotation,
		bool shouldApplyCircularMask
	)
	{
		// TODO: Create an ImageNotFoundException.
		if (!_imageCollectionById.TryGetValue(imageId, out var metadata)) throw new FileNotFoundException();

		// Ensure that we are only provided with animated formats that we actually support. Otherwise, that will cause problems.
		if ((targetAnimatedFormats & ~(ImageFormats.Gif | ImageFormats.Png | ImageFormats.WebPLossy | ImageFormats.WebPLossless)) != 0) throw new ArgumentOutOfRangeException(nameof(targetAnimatedFormats));
		if (sourceRectangle.Left + sourceRectangle.Width > metadata.Width || sourceRectangle.Top + sourceRectangle.Height > metadata.Height) throw new ArgumentException(nameof(sourceRectangle));

		// Determine now if the image already has the correct size. We want to avoid reprocessing an image that may already be correct.
		bool shouldCrop = sourceRectangle.Left != 0 ||
			sourceRectangle.Top != 0 ||
			sourceRectangle.Width != metadata.Width ||
			sourceRectangle.Height != metadata.Height;
		bool shouldResize = targetSize.Width != sourceRectangle.Width ||
			targetSize.Height != sourceRectangle.Height;
		bool isCorrectSize = !(shouldCrop || shouldResize);

		// First and foremost, adjust the animation stripping requirement based on the image and the supported formats of the device.
		bool shouldStripAnimations = metadata.IsAnimated && targetAnimatedFormats == 0;

		bool targetIsStatic = shouldStripAnimations || !metadata.IsAnimated;

		// Then, determine which set of formats should be used based on the image.
		// If the image supports at least one animated format, we should be able to convert to that format.
		var applicableFormats = targetIsStatic ? targetStaticFormats : targetAnimatedFormats;

		// Then, Determine the target image format based on what is allowed.
		ImageFormat targetFormat;

		// First and foremost, if the image is already in a supported format, we will keep that format.
		if (((1U << (int)metadata.Format) & (uint)applicableFormats) != 0)
		{
			targetFormat = metadata.Format;
		}
		// If an animated image needs to be converted to a different format, we will restrict ourselves to a specific subset of formats supporting animations. (GIF and WebP)
		else if (!targetIsStatic)
		{
			// By order of preference: WebP (lossless), PNG, WebP (lossy), GIF
			if ((applicableFormats & ImageFormats.WebPLossless) != 0) targetFormat = ImageFormat.WebPLossless;
			else if ((applicableFormats & ImageFormats.Png) != 0) targetFormat = ImageFormat.Png;
			else if ((applicableFormats & ImageFormats.WebPLossy) != 0) targetFormat = ImageFormat.WebPLossy;
			else if ((applicableFormats & ImageFormats.Gif) != 0) targetFormat = ImageFormat.Gif;
			else throw new UnreachableException("The code must explicitly check each possible animated image format.");
		}
		else
		{
			// By order of preference: WebP Lossless, PNG, Raw, Bitmap, WebP Lossy, Jpeg, Gif
			// We should do some arbitration of formats depending on the image itself (is it lossless or paletted already ?), but this can be done later on.
			if ((applicableFormats & ImageFormats.WebPLossless) != 0) targetFormat = ImageFormat.WebPLossless;
			else if ((applicableFormats & ImageFormats.Png) != 0) targetFormat = ImageFormat.Png;
			else if ((applicableFormats & ImageFormats.Raw) != 0) targetFormat = ImageFormat.Raw;
			else if ((applicableFormats & ImageFormats.Bitmap) != 0) targetFormat = ImageFormat.Bitmap;
			else if ((applicableFormats & ImageFormats.WebPLossy) != 0) targetFormat = ImageFormat.WebPLossy;
			else if ((applicableFormats & ImageFormats.Jpeg) != 0) targetFormat = ImageFormat.Jpeg;
			else if ((applicableFormats & ImageFormats.Gif) != 0) targetFormat = ImageFormat.WebPLossy;
			else throw new UnreachableException("The code must explicitly check each possible animated image format.");
		}

		// If the target format is GIF and the image has a correct size, we disable the circular mask.
		// The reason for that being that we can not guarantee that the processing we would apply would produce a better result than the current image,
		// even if we reduce the number of colors by applying the mask.
		// Until we have more faith in the result of image processing, it is better to trust the original image.
		// If the original image is not good, the user still has the option of optimizing it externally.
		// If we re-processed the image everytime, we would prevent this.
		if (targetFormat == ImageFormat.Gif && isCorrectSize) shouldApplyCircularMask = false;

		var operations = ImageOperations.None;

		if (shouldCrop) operations |= ImageOperations.Crop;
		if (shouldResize) operations |= ImageOperations.Resize;
		if (shouldApplyCircularMask) operations |= ImageOperations.CircularMask;
		if (shouldStripAnimations) operations |= ImageOperations.StripAnimation;

		// ⚠️ Just straight output the rotation here. Multiplication should be optimized to a shift by the JIT, but we keep sync with enum values that way.
		operations |= (ImageOperations)((uint)targetRotation * (uint)ImageOperations.Rotate90);

		Span<byte> payload = stackalloc byte[30];

		payload[0] = (byte)operations;
		payload[1] = (byte)targetFormat;
		LittleEndian.Write(ref payload[2], imageId);
		LittleEndian.Write(ref payload[18], (ushort)sourceRectangle.Left);
		LittleEndian.Write(ref payload[20], (ushort)sourceRectangle.Top);
		LittleEndian.Write(ref payload[22], (ushort)sourceRectangle.Width);
		LittleEndian.Write(ref payload[24], (ushort)sourceRectangle.Height);
		LittleEndian.Write(ref payload[26], (ushort)targetSize.Width);
		LittleEndian.Write(ref payload[28], (ushort)targetSize.Height);

		// Compute a logical image ID to be able to avoid costly transformations.
		// TODO: Of course, we want to persist the logical to physical image IDs at some point, but having it in-memory at least allows for fast image swapping.
		var logicalImageId = XxHash128.HashToUInt128(payload, LogicalImageIdHashSeed);
		if (_logicalImageMetadata.TryGetValue(logicalImageId, out var physicalImageMetadata))
		{
			return (physicalImageMetadata.Id, physicalImageMetadata.Format, GetImageFile(physicalImageMetadata.Id));
		}

		// Shortcut to return the existing physical image if it matches perfectly.
		// Later on, should still bind the transformation metadata to the existing image.
		if (targetFormat == metadata.Format &&
			isCorrectSize &&
			!shouldStripAnimations &&
			!shouldApplyCircularMask)
		{
			RegisterLogicalImageMetadata(logicalImageId, metadata.ToMetadata());
			return (imageId, targetFormat, GetImageFile(imageId));
		}

		UInt128 physicalImageId;
		string temporaryFileName = GetTemporaryFileName(_imageCacheDirectory);
		using (var stream = new FileStream(temporaryFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.None))
		{
			using (var image = GetImageFile(imageId))
			{
				TransformImage(stream, operations, image, sourceRectangle, targetFormat, targetPixelFormat, targetSize);
			}
			stream.Flush();
			// Yeah, I'm doing all of this just because I don't want to go through the stream abstraction to compute the Hash.
			// I have no idea if this is better, but I believe that in many cases, the file should entirely be in the kernel cache at that point, so it could be marginally faster.
			using (var memoryMappedFile = MemoryMappedFile.CreateFromFile(stream, null, stream.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, true))
			using (var memoryManager = new MemoryMappedFileMemoryManager(memoryMappedFile, 0, checked((int)stream.Length), MemoryMappedFileAccess.Read))
			{
				physicalImageId = XxHash128.HashToUInt128(memoryManager.GetSpan(), PhysicalImageIdHashSeed);
			}
			string fileName = GetFileName(_imageCacheDirectory, physicalImageId);
			// We save on memory (especially memory management) by directly writing the file to disk, however, we can only compute the final filename after the whole data has been written.
			// We'll assume that if a file exists, it is already correct. In that case, we'll just delete the temporary file.
			// This is kinda taking the bet that the file won't already exist, though.
			if (File.Exists(fileName))
			{
				File.Delete(temporaryFileName);
			}
			else
			{
				File.Move(temporaryFileName, fileName, false);
			}
		}
		physicalImageMetadata = new(physicalImageId, (ushort)targetSize.Width, (ushort)targetSize.Height, targetFormat, !targetIsStatic);
		RegisterLogicalImageMetadata(logicalImageId, physicalImageMetadata);
		return (physicalImageId, targetFormat, GetImageFile(physicalImageId));
	}

	private void RegisterLogicalImageMetadata(UInt128 logicalImageId, ImageMetadata metadata)
	{
		if (_logicalImageMetadata.TryAdd(logicalImageId, metadata))
		{
			_logicalImageMetadataPersistenceWriter.TryWrite(new(logicalImageId, metadata));
		}
	}

	// The method below is basically a specialized transformation to generate a physical image for a given device specification.
	private void TransformImage
	(
		Stream stream,
		ImageOperations operations,
		ImageFile originalImage,
		Rectangle sourceRectangle,
		ImageFormat targetFormat,
		PixelFormat targetPixelFormat,
		Size targetSize
	)
	{
		SixLabors.ImageSharp.Image image;
		using (var sourceStream = originalImage.CreateReadStream())
		{
			image = SixLabors.ImageSharp.Image.Load(sourceStream);
		}
		try
		{
			if (sourceRectangle.Left + sourceRectangle.Width > image.Width || sourceRectangle.Top + sourceRectangle.Height > image.Height) throw new ArgumentException(nameof(sourceRectangle));

			var resampler = KnownResamplers.Bicubic;
			var maskColor = SixLabors.ImageSharp.Color.Black;
			if (image.Metadata.TryGetFormatMetadata(SixLabors.ImageSharp.Formats.Gif.GifFormat.Instance, out var gifMetadata))
			{
				var isResampling = targetSize.Width != sourceRectangle.Width || targetSize.Height != sourceRectangle.Height;

				if (!isResampling) resampler = KnownResamplers.NearestNeighbor;

				// This is certainly not perfect. A global color would probably be a better choice if there are less local colors than global colors.
				if (gifMetadata!.GlobalColorTable is not null)
				{
					maskColor = gifMetadata.GlobalColorTable.GetValueOrDefault().Span[gifMetadata.BackgroundColorIndex];
					// Clear the palette if we intend to resample the image.
					// Hopefully, this will produce better end results.
					if (isResampling) gifMetadata.GlobalColorTable = null;
				}
				else
				{
					if (image.Frames[0].Metadata.TryGetFormatMetadata(SixLabors.ImageSharp.Formats.Gif.GifFormat.Instance, out var frameMetadata) &&
						frameMetadata!.LocalColorTable is { } localColorTable)
					{
						maskColor = localColorTable.Span[frameMetadata.HasTransparency ? frameMetadata.TransparencyIndex : 0];
					}

					// Same as for the global metadata, clear the local palette for every frame.
					// NB: This might break some images? (Thinking of true color gifs)
					if (isResampling)
					{
						foreach (var frame in image.Frames)
						{
							if (frame.Metadata.TryGetFormatMetadata(SixLabors.ImageSharp.Formats.Gif.GifFormat.Instance, out frameMetadata))
							{
								frameMetadata!.LocalColorTable = null;
							}
						}
					}
				}
			}

			if ((operations & ImageOperations.StripAnimation) != 0 && image.Frames.Count > 1)
			{
				// TODO: Also clear frame metadata related to animation.
				do
				{
					image.Frames.RemoveFrame(image.Frames.Count - 1);
				}
				while (image.Frames.Count > 1);
			}

			image.Mutate
			(
				ctx =>
				{
					// TODO: Merge this with rotation.
					// Basically, it should be possible to avoid explicit rotation if the requested rotation already matches the EXIF rotation.
					// Meaning that EXIF rotation should be added to image metadata.
					ctx.AutoOrient();

					if ((operations & ImageOperations.Crop) != 0) ctx.Crop(new(sourceRectangle.Left, sourceRectangle.Top, sourceRectangle.Width, sourceRectangle.Height));
					if ((operations & ImageOperations.Resize) != 0)
					{
						ctx.Resize
						(
							targetSize.Width,
							targetSize.Height,
							resampler,
							true
						);
					}
					var rotation = (ImageRotation)((uint)(operations & ImageOperations.Rotate270) / (uint)ImageOperations.Rotate90);
					if (rotation != ImageRotation.None)
					{
						ctx.Rotate((RotateMode)(90 * (uint)rotation));
					}
					if ((operations & ImageOperations.CircularMask) != 0)
					{
						// TODO: For GIF, selected color could still be improved to pick a pixel already within the circle.
						ctx.ApplyProcessor(new CircleCroppingProcessor(maskColor, (byte)(targetFormat == ImageFormat.Jpeg ? 3 : 2)));
					}
				}
			);

			switch (targetFormat)
			{
			case ImageFormat.Raw:
				if (targetPixelFormat == PixelFormat.B8G8R8A8 || targetPixelFormat == PixelFormat.B8G8R8X8) SaveRawImage<Bgra32>(image, stream);
				else if (targetPixelFormat == PixelFormat.R8G8B8A8 || targetPixelFormat == PixelFormat.R8G8B8X8) SaveRawImage<Rgba32>(image, stream);
				else if (targetPixelFormat == PixelFormat.A8R8G8B8 || targetPixelFormat == PixelFormat.X8R8G8B8) SaveRawImage<Argb32>(image, stream);
				else if (targetPixelFormat == PixelFormat.A8B8G8R8 || targetPixelFormat == PixelFormat.X8B8G8R8) SaveRawImage<Abgr32>(image, stream);
				else if (targetPixelFormat == PixelFormat.B8G8R8) SaveRawImage<Bgr24>(image, stream);
				else if (targetPixelFormat == PixelFormat.R8G8B8) SaveRawImage<Rgb24>(image, stream);
				else throw new NotSupportedException("The specified image format is not yet supported.");
				break;
			case ImageFormat.Bitmap:
				if (targetPixelFormat == PixelFormat.B8G8R8A8 || targetPixelFormat == PixelFormat.B8G8R8X8) SaveBitmapImage<Bgra32>(image, stream, SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel.Pixel32);
				else if (targetPixelFormat == PixelFormat.R8G8B8A8 || targetPixelFormat == PixelFormat.R8G8B8X8) SaveBitmapImage<Rgba32>(image, stream, SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel.Pixel32);
				else if (targetPixelFormat == PixelFormat.A8R8G8B8 || targetPixelFormat == PixelFormat.X8R8G8B8) SaveBitmapImage<Argb32>(image, stream, SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel.Pixel32);
				else if (targetPixelFormat == PixelFormat.A8B8G8R8 || targetPixelFormat == PixelFormat.X8B8G8R8) SaveBitmapImage<Abgr32>(image, stream, SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel.Pixel32);
				else if (targetPixelFormat == PixelFormat.B8G8R8) SaveBitmapImage<Bgr24>(image, stream, SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel.Pixel24);
				else if (targetPixelFormat == PixelFormat.R8G8B8) SaveBitmapImage<Rgb24>(image, stream, SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel.Pixel24);
				else throw new NotSupportedException("The specified image format is not yet supported.");
				break;
			case ImageFormat.Gif:
				SixLabors.ImageSharp.ImageExtensions.SaveAsGif(image, stream);
				break;
			case ImageFormat.Png:
				// TODO: Take into account the pixel format here.
				SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, stream);
				break;
			case ImageFormat.Jpeg:
				// TODO: Should probably provide a way to control encoding quality here. The defaults might be fine for now.
				SixLabors.ImageSharp.ImageExtensions.SaveAsJpeg(image, stream);
				break;
			default:
				throw new NotImplementedException();
			}
		}
		finally
		{
			image.Dispose();
		}
	}

	private static void SaveBitmapImage<TPixel>(SixLabors.ImageSharp.Image image, Stream stream, SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel bitsPerPixel)
		where TPixel : unmanaged, IPixel<TPixel>
	{
		var encoder = new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder
		{
			BitsPerPixel = bitsPerPixel
		};

		if (image is SixLabors.ImageSharp.Image<TPixel> imageInCorrectFormat)
		{
			SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(imageInCorrectFormat, stream, encoder);
		}
		else
		{
			using (imageInCorrectFormat = image.CloneAs<TPixel>())
			{
				SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(imageInCorrectFormat, stream, encoder);
			}
		}
	}

	private static void SaveRawImage<TPixel>(SixLabors.ImageSharp.Image image, Stream stream)
		where TPixel : unmanaged, IPixel<TPixel>
	{
		if (image is SixLabors.ImageSharp.Image<TPixel> imageInCorrectFormat)
		{
			WriteFrameDataToStream(stream, imageInCorrectFormat.Frames[0]);
		}
		else
		{
			using (imageInCorrectFormat = image.CloneAs<TPixel>())
			{
				WriteFrameDataToStream(stream, imageInCorrectFormat.Frames[0]);
			}
		}
	}

	private static void WriteFrameDataToStream<TPixel>(Stream stream, SixLabors.ImageSharp.ImageFrame<TPixel> frame)
		where TPixel : unmanaged, IPixel<TPixel>
	{
		int height = frame.Height;
		for (int i = 0; i < height; i++)
		{
			var row = frame.DangerousGetPixelRowMemory(i);
			stream.Write(MemoryMarshal.Cast<TPixel, byte>(row.Span));
		}
	}

	public async ValueTask<bool> HasImageAsync(string imageName, CancellationToken cancellationToken)
	{
		if (!ImageNameSerializer.IsNameValid(imageName)) throw new ArgumentException("Invalid name.");

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			return _imageCollection.ContainsKey(imageName);
		}
	}

	public async ValueTask AddImageAsync(string imageName, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		if (!ImageNameSerializer.IsNameValid(imageName)) throw new ArgumentException("Invalid name.");
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (_imageCollection.ContainsKey(imageName)) throw new ArgumentException("Name already in use.");

			var info = SixLabors.ImageSharp.Image.Identify(data.Span);

			ImageFormat imageFormat;
			bool isAnimated = false;

			if (info.Metadata.DecodedImageFormat == SixLabors.ImageSharp.Formats.Bmp.BmpFormat.Instance)
			{
				imageFormat = ImageFormat.Bitmap;
			}
			else if (info.Metadata.DecodedImageFormat == SixLabors.ImageSharp.Formats.Gif.GifFormat.Instance)
			{
				imageFormat = ImageFormat.Gif;
				isAnimated = info.FrameMetadataCollection.Count > 1;
			}
			else if (info.Metadata.DecodedImageFormat == SixLabors.ImageSharp.Formats.Png.PngFormat.Instance)
			{
				imageFormat = ImageFormat.Png;
				isAnimated = info.FrameMetadataCollection.Count > 1;
			}
			else if (info.Metadata.DecodedImageFormat == SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance)
			{
				imageFormat = ImageFormat.Jpeg;
			}
			else if (info.Metadata.DecodedImageFormat == SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance)
			{
				imageFormat = info.Metadata.GetFormatMetadata(SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance).FileFormat switch
				{
					SixLabors.ImageSharp.Formats.Webp.WebpFileFormatType.Lossless => ImageFormat.WebPLossless,
					SixLabors.ImageSharp.Formats.Webp.WebpFileFormatType.Lossy => ImageFormat.WebPLossy,
					_ => throw new UnreachableException()
				};
				isAnimated = info.FrameMetadataCollection.Count > 1;
			}
			else
			{
				throw new InvalidDataException("Invalid image format.");
			}

			var metadata = new LiveImageMetadata
			(
				XxHash128.HashToUInt128(data.Span, PhysicalImageIdHashSeed),
				imageName,
				checked((ushort)info.Width),
				checked((ushort)info.Height),
				imageFormat,
				isAnimated
			);

			string fileName = GetFileName(_imageCacheDirectory, metadata.Id);
			if (File.Exists(fileName)) throw new InvalidOperationException("An image with the same data already exists.");

			await _imagesConfigurationContainer.WriteValueAsync
			(
				imageName,
				metadata.ToMetadata(),
				cancellationToken
			).ConfigureAwait(false);

			await File.WriteAllBytesAsync(fileName, data, cancellationToken).ConfigureAwait(false);

			_imageCollection.Add(imageName, metadata);
			if (!_imageCollectionById.TryAdd(metadata.Id, metadata)) throw new UnreachableException();

			if (Volatile.Read(ref _changeListeners) is { } changeListeners)
			{
				changeListeners.TryWrite(new(WatchNotificationKind.Addition, new(metadata.Id, imageName, fileName, metadata.Width, metadata.Height, metadata.Format, metadata.IsAnimated)));
			}
		}
	}

	public async ValueTask RemoveImageAsync(string imageName, CancellationToken cancellationToken)
	{
		if (!ImageNameSerializer.IsNameValid(imageName)) throw new ArgumentException("Invalid name.");
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (_imageCollection.TryGetValue(imageName, out var metadata))
			{
				// TODO: There should be a check to ensure that images are not in use.
				// Ideally, by calling all dependent services to do an exhaustive verification. (As removing images is an exceptional event)
				// Problem: How to do this without introducing a deadlock.
				string fileName = GetFileName(_imageCacheDirectory, metadata.Id);
				File.Delete(fileName);
				_imageCollection.Remove(imageName);
				_imageCollectionById.TryRemove(metadata.Id, out _);

				if (Volatile.Read(ref _changeListeners) is { } changeListeners)
				{
					changeListeners.TryWrite(new(WatchNotificationKind.Removal, new(metadata.Id, imageName, fileName, metadata.Width, metadata.Height, metadata.Format, metadata.IsAnimated)));
				}
			}
		}
	}
}

[Flags]
internal enum ImageOperations : byte
{
	None = 0,

	// One bit these individual operations that can be applied to images for device adaptation.
	Crop = 1,
	Resize = 2,
	CircularMask = 4,
	StripAnimation = 8,

	// Two bits for rotations
	// ⚠️ Be careful that these values must be synchronized with ImageRotation. We must be able to quickly insert and extract those.
	Rotate90 = 16,
	Rotate180 = 32,
	Rotate270 = 48,
}
