namespace Exo.Devices.Nzxt.Kraken;

internal readonly struct ScreenInformation
{
	public ScreenInformation(byte currentBrightness, byte imageCount, ushort width, ushort height)
	{
		CurrentBrightness = currentBrightness;
		ImageCount = imageCount;
		Width = width;
		Height = height;
	}

	public byte CurrentBrightness { get; }
	public byte ImageCount { get; }
	public ushort Width { get; }
	public ushort Height { get; }
}
