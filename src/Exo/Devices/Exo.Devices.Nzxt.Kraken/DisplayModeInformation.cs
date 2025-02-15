namespace Exo.Devices.Nzxt.Kraken;

// ⚠️ The fact that this structure is 16 bytes is relied upon in KrakenDisplayManager.
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
