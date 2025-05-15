namespace Exo.Service.Configuration;

[TypeId(0xFA1692D2, 0x3E25, 0x4DEC, 0x95, 0x36, 0x56, 0xA6, 0x37, 0xCA, 0x45, 0x76)]
internal readonly struct PersistedLightDeviceInformation
{
	public required LightDeviceCapabilities Capabilities { get; init; }
}
