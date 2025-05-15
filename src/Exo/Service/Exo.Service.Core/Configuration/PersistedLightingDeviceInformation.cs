using Exo.Lighting;

namespace Exo.Service.Configuration;

[TypeId(0x8EF5FD05, 0x960B, 0x449C, 0xA2, 0x01, 0xC6, 0x58, 0x99, 0x00, 0x20, 0x8E)]
internal readonly struct PersistedLightingDeviceInformation
{
	public BrightnessCapabilities? BrightnessCapabilities { get; init; }
	public Guid? UnifiedLightingZoneId { get; init; }
	public LightingPersistenceMode PersistenceMode { get; init; }
}
