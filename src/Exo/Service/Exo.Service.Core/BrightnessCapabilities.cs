using System.Text.Json.Serialization;

namespace Exo.Service;

public readonly record struct BrightnessCapabilities
{
	[JsonConstructor]
	public BrightnessCapabilities(byte minimumValue, byte maximumValue)
	{
		MinimumValue = minimumValue;
		MaximumValue = maximumValue;
	}

	public byte MinimumValue { get; }
	public byte MaximumValue { get; }
}
