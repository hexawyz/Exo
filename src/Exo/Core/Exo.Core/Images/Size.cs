namespace Exo.Images;

[TypeId(0xBA4846D8, 0xEE08, 0x4AE2, 0xAE, 0x48, 0xF7, 0x47, 0xAB, 0x02, 0xAC, 0x6F)]
public readonly record struct Size
{
	public int Width { get; init; }
	public int Height { get; init; }

	public Size() { }

	public Size(int width, int height) => (Width, Height) = (width, height);
}
