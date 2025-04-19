namespace Exo.Images;

[TypeId(0xE9D65D40, 0x66E7, 0x4AED, 0xBB, 0x53, 0x4F, 0xB4, 0xAA, 0xE8, 0xE5, 0x36)]
public readonly record struct Point
{
	public int X { get; init; }
	public int Y { get; init; }

	public Point() { }

	public Point(int x, int y) => (X, Y) = (x, y);
}
