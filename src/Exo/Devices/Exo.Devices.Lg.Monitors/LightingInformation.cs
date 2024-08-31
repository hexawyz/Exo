namespace Exo.Devices.Lg.Monitors;

public readonly struct LightingInformation
{
	public bool IsLightingEnabled { get; }
	public byte MinimumBrightnessLevel { get; }
	public byte MaximumBrightnessLevel { get; }
	public byte CurrentBrightnessLevel { get; }

	public LightingInformation(bool isLightingEnabled, byte minimumBrightnessLevel, byte maximumBrightnessLevel, byte currentBrightnessLevel)
	{
		IsLightingEnabled = isLightingEnabled;
		MinimumBrightnessLevel = minimumBrightnessLevel;
		MaximumBrightnessLevel = maximumBrightnessLevel;
		CurrentBrightnessLevel = currentBrightnessLevel;
	}
}
