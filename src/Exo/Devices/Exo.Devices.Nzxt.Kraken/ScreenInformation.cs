namespace Exo.Devices.Nzxt.Kraken;

internal readonly struct ScreenInformation
{
	public ScreenInformation(byte currentBrightness, byte imageCount, ushort width, ushort height, ushort memoryBlockCount)
	{
		CurrentBrightness = currentBrightness;
		ImageCount = imageCount;
		Width = width;
		Height = height;
		MemoryBlockCount = memoryBlockCount;
	}

	public byte CurrentBrightness { get; }
	public byte ImageCount { get; }
	public ushort Width { get; }
	public ushort Height { get; }
	public ushort MemoryBlockCount { get; }
}
