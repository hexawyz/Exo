namespace DeviceTools.Logitech.HidPlusPlus;

public readonly struct BacklightState
{
	public BacklightState(byte currentLevel, byte levelCount)
	{
		CurrentLevel = currentLevel;
		LevelCount = levelCount;
	}

	public byte CurrentLevel { get; }
	public byte LevelCount { get; }
}
