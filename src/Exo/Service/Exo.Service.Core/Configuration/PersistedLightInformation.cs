namespace Exo.Service.Configuration;

[TypeId(0x1DA2BCD6, 0xE0F8, 0x49D8, 0xA1, 0x3D, 0xB4, 0x66, 0x81, 0x80, 0x72, 0x19)]
internal readonly struct PersistedLightInformation
{
	public required LightCapabilities Capabilities { get; init; }
	public required byte MinimumBrightness { get; init; }
	public required byte MaximumBrightness { get; init; }
	public required uint MinimumTemperature { get; init; }
	public required uint MaximumTemperature { get; init; }
}
