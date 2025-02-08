using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Hashing;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Images;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using SixLabors.ImageSharp.Processing;

namespace Exo.Service;

internal sealed class ImageStorageService
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

		return new(logger, imagesConfigurationContainer, imageCacheDirectory, imageCollection, imageCollectionById);
	}

	private const long PhysicalImageIdHashSeed = unchecked((long)0x90AB71E534FD62C8U);

	private readonly Dictionary<string, LiveImageMetadata> _imageCollection;
	private readonly ConcurrentDictionary<UInt128, LiveImageMetadata> _imageCollectionById;
	private readonly ConcurrentDictionary<UInt128, SharedImageSlot> _openImageFiles;
	private readonly IConfigurationContainer<string> _imagesConfigurationContainer;
	private readonly string _imageCacheDirectory;
	private ChannelWriter<ImageChangeNotification>[]? _changeListeners;
	private readonly AsyncLock _lock;
	private readonly ILogger<ImageStorageService> _logger;

	private ImageStorageService
	(
		ILogger<ImageStorageService> logger,
		IConfigurationContainer<string> imagesConfigurationContainer,
		string imageCacheDirectory,
		Dictionary<string, LiveImageMetadata> imageCollection,
		ConcurrentDictionary<UInt128, LiveImageMetadata> imageCollectionById
	)
	{
		_logger = logger;
		_imagesConfigurationContainer = imagesConfigurationContainer;
		_imageCacheDirectory = imageCacheDirectory;
		_imageCollection = imageCollection;
		_imageCollectionById = imageCollectionById;
		_openImageFiles = new();
		_lock = new();
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

	// NB: Transformations (that are cached) should actually be implemented using an indirection.
	// In all cases, image IDs cached on disk should be computed based on image contents.
	// This means that there would be two image ID namespaces, which is somewhat of a problem.
	// From the target implementation we want for images, I can establish these rules:
	//  - There should be logical images and physical images.
	//  - Each logical image can optionally be persisted into a physical image
	//  - Logical images should only reference other logical images
	//  - Exception: Physical *named* images should be referenced by a special kind of logical image, acting as an indirection.
	//  - All physical images are either a named image OR a specific serialization process applied on a logical image.
	//  - Any physical image can be referenced by multiple logical images
	//  - The mapping between logical and physical images should always be persisted. (If the physical image is persisted)
	// TODO: The above
	// The method below is basically a specialized transformation to generate a physical image for a gicen device specification.
	// These images are intended to be sent straight to devices in their binary encoded format.
	public (UInt128 ImageId, ImageFormat format, ImageFile Image) GetTransformedImage
	(
		UInt128 imageId,
		Rectangle sourceRectangle,
		ImageFormats targetStaticFormats,
		ImageFormats targetAnimatedFormats,
		Size targetSize,
		bool shouldApplyCircularMask
	)
	{
		// TODO: Create an ImageNotFoundException.
		if (!_imageCollectionById.TryGetValue(imageId, out var metadata)) throw new FileNotFoundException();

		// Ensure that we are only provided with animated formats that we actually support. Otherwise, that will cause problems.
		if ((targetAnimatedFormats & ~(ImageFormats.Gif | ImageFormats.Png | ImageFormats.WebPLossy | ImageFormats.WebPLossless)) != 0) throw new ArgumentOutOfRangeException(nameof(targetAnimatedFormats));
		if (sourceRectangle.Left + sourceRectangle.Width > metadata.Width || sourceRectangle.Top + sourceRectangle.Height > metadata.Height) throw new ArgumentException(nameof(sourceRectangle));

		// First and foremost, adjust the animation stripping requirement based on the image and the supported formats of the device.
		bool shouldStripAnimations = metadata.IsAnimated && targetAnimatedFormats == 0;

		// Then, determine which set of formats should be used based on the image.
		// If the image supports at least one animated format, we should be able to convert to that format.
		var applicableFormats = shouldStripAnimations ? targetStaticFormats : targetAnimatedFormats;

		// Then, Determine the target image format based on what is allowed.
		ImageFormat targetFormat;

		// First and foremost, if the image is already in a supported format, we will keep that format.
		if (((1U << (int)metadata.Format) & (uint)applicableFormats) != 0)
		{
			targetFormat = metadata.Format;
		}
		// If an animated image needs to be converted to a different format, we will restrict ourselves to a specific subset of formats supporting animations. (GIF and WebP)
		else if (!shouldStripAnimations)
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

		Span<byte> payload = stackalloc byte[30];

		payload[0] = (byte)
		(
			shouldApplyCircularMask ?
				(shouldStripAnimations ? ImageOperationType.CircularTargetAdjustStripAnimation : ImageOperationType.CircularTargetAdjust) :
				(shouldStripAnimations ? ImageOperationType.RectangularTargetAdjustStripAnimation : ImageOperationType.RectangularTargetAdjust)
		);

		payload[1] = (byte)targetFormat;
		LittleEndian.Write(ref payload[2], imageId);
		LittleEndian.Write(ref payload[18], (ushort)sourceRectangle.Left);
		LittleEndian.Write(ref payload[20], (ushort)sourceRectangle.Top);
		LittleEndian.Write(ref payload[22], (ushort)sourceRectangle.Width);
		LittleEndian.Write(ref payload[24], (ushort)sourceRectangle.Height);
		LittleEndian.Write(ref payload[26], (ushort)targetSize.Width);
		LittleEndian.Write(ref payload[28], (ushort)targetSize.Height);

		// NB: Let's use that later
		//var transformedImageId = XxHash128.HashToUInt128(payload);
		//if (TryGetImageFile(transformedImageId) is { } transformedImage) return transformedImage;

		// Shortcut to return the existing physical image if it matches perfectly.
		// Later on, should still bind the transformation metadata to the existing image.
		if (targetFormat == metadata.Format &&
			sourceRectangle.Left == 0 &&
			sourceRectangle.Top == 0 &&
			sourceRectangle.Width == metadata.Width &&
			sourceRectangle.Height == metadata.Height &&
			targetSize.Width == metadata.Width &&
			targetSize.Height == metadata.Height &&
			!shouldStripAnimations &&
			!shouldApplyCircularMask)
		{
			return (imageId, targetFormat, GetImageFile(imageId));
		}

		using (var image = GetImageFile(imageId))
		using (var stream = new MemoryStream())
		{
			TransformImage(stream, image, sourceRectangle, targetFormat, targetSize, shouldStripAnimations, shouldApplyCircularMask);
			var physicalImageId = XxHash128.HashToUInt128(stream.GetBuffer().AsSpan(0, (int)stream.Length), PhysicalImageIdHashSeed);
			string fileName = GetFileName(_imageCacheDirectory, physicalImageId);
			File.WriteAllBytes(fileName, stream.GetBuffer().AsSpan(0, (int)stream.Length));
			return (physicalImageId, targetFormat, GetImageFile(physicalImageId));
		}
	}

	// TODO: Improve this method. Current operation is quick and dirty, but there are certainly better possibilities.
	private void TransformImage(Stream stream, ImageFile originalImage, Rectangle sourceRectangle, ImageFormat targetFormat, Size targetSize, bool shouldStripAnimations, bool applyCircularMask)
	{
		using (var memoryManager = originalImage.CreateMemoryManager())
		{
			var image = SixLabors.ImageSharp.Image.Load(memoryManager.GetSpan());

			if (sourceRectangle.Left + sourceRectangle.Width > image.Width || sourceRectangle.Top + sourceRectangle.Height > image.Height) throw new ArgumentException(nameof(sourceRectangle));

			image.Mutate
			(
				ctx =>
				{
					ctx.AutoOrient()
						.Crop(new(sourceRectangle.Left, sourceRectangle.Top, sourceRectangle.Width, sourceRectangle.Height))
						.Resize(targetSize.Width, targetSize.Height, true);
					if (applyCircularMask)
					{
						ctx.ApplyProcessor(new CircleCroppingProcessor(SixLabors.ImageSharp.Color.Black, (byte)(targetFormat == ImageFormat.Jpeg ? 3 : 0)));
					}
				}
			);

			switch (targetFormat)
			{
			case ImageFormat.Raw:
				// Need to provide the pixel format here.
				throw new NotImplementedException();
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

			await _imagesConfigurationContainer.WriteValueAsync(imageName, metadata, cancellationToken).ConfigureAwait(false);

			await File.WriteAllBytesAsync(fileName, data, cancellationToken).ConfigureAwait(false);

			_imageCollection.Add(imageName, metadata);

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
				string fileName = GetFileName(_imageCacheDirectory, metadata.Id);
				File.Delete(fileName);
				_imageCollection.Remove(imageName);

				if (Volatile.Read(ref _changeListeners) is { } changeListeners)
				{
					changeListeners.TryWrite(new(WatchNotificationKind.Removal, new(metadata.Id, imageName, fileName, metadata.Width, metadata.Height, metadata.Format, metadata.IsAnimated)));
				}
			}
		}
	}
}

internal enum ImageOperationType : byte
{
	Unknown = 0,
	RectangularTargetAdjust = 1,
	RectangularTargetAdjustStripAnimation = 2,
	CircularTargetAdjust = 3,
	CircularTargetAdjustStripAnimation = 4,
}
