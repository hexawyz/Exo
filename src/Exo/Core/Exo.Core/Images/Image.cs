namespace Exo.Images;

/// <summary>Represents an image that can be rasterized.</summary>
/// <remarks>
/// <para>
/// The best way to understand <see cref="Image"/>, is that an <see cref="Image"/> instance is a recipe for creating a bitmap.
/// A bitmap image is obtained by calling the method <see cref="Rasterize(Size)"/>.
/// </para>
/// <para>
/// Images dimensions are always assumed to be in pixels, but there are two types of images that will attach a different meaning for the <see cref="Size"/> property.
/// The two types of images are "scaled" and "anchored".
/// The type of image determines how it will be drawn in its target size, either during rasterization or when integrated into another image.
/// </para>
/// </remarks>
[TypeId(0x0BB0B78A, 0xBEE5, 0x4A53, 0x90, 0x0E, 0x1F, 0x1A, 0x09, 0x1C, 0x3A, 0x00)]
public abstract class Image
{
	/// <summary>Gets a size of the image, that can be either the reference size or the minimum size.</summary>
	/// <remarks>
	/// <para>
	/// If <see cref="Type"/> is <see cref="ImageType.Scaled"/>, this represents the reference size.
	/// If <see cref="Type"/> is <see cref="ImageType.Anchored"/>, this represents the minimum size.
	/// </para>
	/// <para>
	/// In the case of anchored image, the minimum size, expressed by this property, represents the size at which the rendering of the image can produce a meaningful result.
	/// For example, a rectangle with a stroke width of 4 can only be rendered meaningfully in an image of at least 9x9 pixels.
	/// It is acceptable for an anchored image to have a minimum size of <c>0x0</c>.
	/// Scaled images, however, should always have a size of at least <c>1x1</c>.
	/// </para>
	/// </remarks>
	public abstract Size Size { get; }

	/// <summary>Gets the type of image of this instance.</summary>
	public abstract ImageType Type { get; }

	/// <summary>Rasterizes this image into a bitmap.</summary>
	/// <param name="size">The target size of the bitmap.</param>
	/// <returns></returns>
	public abstract RasterizedImage Rasterize(Size size);
}

public abstract class RasterizedImage
{
	public abstract ReadOnlyMemory<byte> GetRawBytes();
}
