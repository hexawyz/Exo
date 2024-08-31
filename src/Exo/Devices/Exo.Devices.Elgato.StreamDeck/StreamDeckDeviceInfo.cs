namespace Exo.Devices.Elgato.StreamDeck;

public readonly record struct StreamDeckDeviceInfo
{
	public StreamDeckDeviceInfo(byte buttonRowCount, byte buttonColumnCount, ushort buttonImageWidth, ushort buttonImageHeight, ushort screensaverImageWidth, ushort screensaverImageHeight)
	{
		ButtonRowCount = buttonRowCount;
		ButtonColumnCount = buttonColumnCount;

		ButtonImageWidth = buttonImageWidth;
		ButtonImageHeight = buttonImageHeight;

		ScreensaverImageWidth = screensaverImageWidth;
		ScreensaverImageHeight = screensaverImageHeight;

		ButtonCount = checked((byte)(ButtonRowCount * ButtonColumnCount));
	}

	public byte ButtonRowCount { get; }
	public byte ButtonColumnCount { get; }

	public ushort ButtonImageWidth { get; }
	public ushort ButtonImageHeight { get; }

	public ushort ScreensaverImageWidth { get; }
	public ushort ScreensaverImageHeight { get; }

	public byte ButtonCount { get; }
}
