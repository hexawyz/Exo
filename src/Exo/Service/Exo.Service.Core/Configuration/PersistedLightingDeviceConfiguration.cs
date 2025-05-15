namespace Exo.Service.Configuration;

[TypeId(0x70F0F081, 0x39F1, 0x4C4C, 0xB5, 0x10, 0x03, 0x7B, 0xDB, 0x14, 0xCB, 0x72)]
internal readonly struct PersistedLightingDeviceConfiguration
{
	public bool IsUnifiedLightingEnabled { get; init; }
	public byte? Brightness { get; init; }
}
