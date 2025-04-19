namespace Exo.Service;

public readonly struct LightInformation(Guid lightId, LightCapabilities capabilities, byte minimumBrightness, byte maximumBrightness, uint minimumTemperature, uint maximumTemperature)
{
	public Guid LightId { get; } = lightId;
	public LightCapabilities Capabilities { get; } = capabilities;
	public byte MinimumBrightness { get; } = minimumBrightness;
	public byte MaximumBrightness { get; } = maximumBrightness;
	public uint MinimumTemperature { get; } = minimumTemperature;
	public uint MaximumTemperature { get; } = maximumTemperature;
}
