namespace Exo.Service;

public readonly record struct PaletteCapabilities
{
	public PaletteCapabilities(ushort colorCount) => ColorCount = colorCount;

	public ushort ColorCount { get; }
}
