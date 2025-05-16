using System.Text.Json.Serialization;
using Exo.Lighting;

namespace Exo.Service.Configuration;

[TypeId(0x8EF5FD05, 0x960B, 0x449C, 0xA2, 0x01, 0xC6, 0x58, 0x99, 0x00, 0x20, 0x8E)]
[method: JsonConstructor]
internal readonly struct PersistedLightingDeviceInformation(BrightnessCapabilities? brightnessCapabilities, Guid? unifiedLightingZoneId, LightingPersistenceMode persistenceMode)
{
	public BrightnessCapabilities? BrightnessCapabilities { get; } = brightnessCapabilities;
	public Guid? UnifiedLightingZoneId { get; } = unifiedLightingZoneId;
	public LightingPersistenceMode PersistenceMode { get; } = persistenceMode;
}
