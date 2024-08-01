namespace Exo.Service;

public readonly record struct BrightnessCapabilities
{
	public required byte MinimumValue { get; init; }
	public required byte MaximumValue { get; init; }
}
