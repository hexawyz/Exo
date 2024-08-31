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

public enum ImageType : byte
{
	Scaled = 0,
	Anchored = 1,
}

[TypeId(0xBA4846D8, 0xEE08, 0x4AE2, 0xAE, 0x48, 0xF7, 0x47, 0xAB, 0x02, 0xAC, 0x6F)]
public readonly record struct Size
{
	public int Width { get; init; }
	public int Height { get; init; }

	public Size() { }

	public Size(int width, int height) => (Width, Height) = (width, height);
}

[TypeId(0xE9D65D40, 0x66E7, 0x4AED, 0xBB, 0x53, 0x4F, 0xB4, 0xAA, 0xE8, 0xE5, 0x36)]
public readonly record struct Point
{
	public int X { get; init; }
	public int Y { get; init; }

	public Point() { }

	public Point(int x, int y) => (X, Y) = (x, y);
}

[TypeId(0x712CCD71, 0x1E7C, 0x4E2E, 0x84, 0x7F, 0xCF, 0x14, 0x97, 0x0D, 0xA4, 0xF9)]
public readonly record struct Rectangle
{
	public int Left { get; init; }
	public int Top { get; init; }
	public int Width { get; init; }
	public int Height { get; init; }

	public Rectangle() { }

	public Rectangle(int left, int top, int width, int height) => (Left, Top, Width, Height) = (left, top, width, height);
	public Rectangle(Point point, Size size) : this(point.X, point.Y, size.Width, size.Height) { }
}

[TypeId(0x9B4D21B7, 0x0727, 0x4F86, 0xA8, 0x19, 0x0F, 0x82, 0x21, 0xD6, 0x9E, 0x5E)]
public readonly record struct Thickness
{
	public int Left { get; init; }
	public int Top { get; init; }
	public int Right { get; init; }
	public int Bottom { get; init; }

	public Thickness() { }

	public Thickness(int left, int top, int width, int height) => (Left, Top, Right, Bottom) = (left, top, width, height);
}
