namespace Exo.Images;

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
