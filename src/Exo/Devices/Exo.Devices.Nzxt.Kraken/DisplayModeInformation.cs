namespace Exo.Devices.Nzxt.Kraken;

internal readonly struct DisplayModeInformation
{
	public DisplayModeInformation(KrakenDisplayMode displayMode, byte imageIndex)
	{
		DisplayMode = displayMode;
		ImageIndex = imageIndex;
	}

	public KrakenDisplayMode DisplayMode { get; }
	public byte ImageIndex { get; }
}
