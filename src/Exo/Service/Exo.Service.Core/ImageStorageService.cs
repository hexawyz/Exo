using System.Globalization;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Images;
using Microsoft.Extensions.Logging;

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

		var imageCollection = new Dictionary<string, ImageMetadata>(StringComparer.OrdinalIgnoreCase);

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
				imageCollection.Add(imageName, result.Value);
			}
		}

		return new(logger, imagesConfigurationContainer, imageCacheDirectory, imageCollection);
	}

	private readonly Dictionary<string, ImageMetadata> _imageCollection;
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
		Dictionary<string, ImageMetadata> imageCollection
	)
	{
		_logger = logger;
		_imagesConfigurationContainer = imagesConfigurationContainer;
		_imageCacheDirectory = imageCacheDirectory;
		_imageCollection = imageCollection;
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
			}
			else if (info.Metadata.DecodedImageFormat == SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance)
			{
				imageFormat = ImageFormat.Jpeg;
			}
			else if (info.Metadata.DecodedImageFormat == SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance)
			{
				imageFormat = info.Metadata.GetFormatMetadata(SixLabors.ImageSharp.Formats.Webp.WebpFormat.Instance).FileFormat == SixLabors.ImageSharp.Formats.Webp.WebpFileFormatType.Lossless ?
					ImageFormat.WebPLossless :
					ImageFormat.WebPLossy;
				isAnimated = info.FrameMetadataCollection.Count > 1;
			}
			else
			{
				throw new InvalidDataException("Invalid image format.");
			}

			var metadata = new ImageMetadata
			(
				XxHash128.HashToUInt128(data.Span, unchecked((long)0x90AB71E534FD62C8U)),
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
