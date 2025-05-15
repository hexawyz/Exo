namespace Exo.Service.Configuration;

[TypeId(0x30F5EF48, 0x2C47, 0x465D, 0x97, 0xD6, 0x86, 0xFF, 0x1C, 0xBD, 0x22, 0xCA)]
internal readonly struct PersistedSensorConfiguration
{
	public string? FriendlyName { get; init; }
	public bool IsFavorite { get; init; }
}
