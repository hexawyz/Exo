using Exo.Images;

namespace Exo.Service;

internal readonly struct ImageInformation(UInt128 imageId, string imageName, string fileName, ushort width, ushort height, ImageFormat format, bool isAnimated)
{
	public UInt128 ImageId { get; } = imageId;
	public string ImageName { get; } = imageName;
	public string FileName { get; } = fileName;
	public ushort Width { get; } = width;
	public ushort Height { get; } = height;
	public ImageFormat Format { get; } = format;
	public bool IsAnimated { get; } = isAnimated;
}
