namespace Exo.Service;

public readonly record struct BrightnessCapabilities
{
	public BrightnessCapabilities(byte minimumValue, byte maximumValue)
	{
		MinimumValue = minimumValue;
		MaximumValue = maximumValue;
	}

	public byte MinimumValue { get; }
	public byte MaximumValue { get; }
}
