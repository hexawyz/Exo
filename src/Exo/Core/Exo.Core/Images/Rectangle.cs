namespace Exo.Images;

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
