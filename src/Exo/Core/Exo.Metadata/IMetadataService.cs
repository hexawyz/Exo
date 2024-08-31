using System.Globalization;

namespace Exo.Metadata;

public interface IMetadataService
{
	string? GetString(CultureInfo? culture, Guid stringId);
	bool TryGetLightingEffectMetadata(string driverKey, string compatibleId, Guid lightingEffectId, out LightingEffectMetadata value);
	bool TryGetLightingZoneMetadata(string driverKey, string compatibleId, Guid lightingZoneId, out LightingZoneMetadata value);
	bool TryGetSensorMetadata(string driverKey, string compatibleId, Guid sensorId, out SensorMetadata value);
	bool TryGetCoolerMetadata(string driverKey, string compatibleId, Guid coolerId, out CoolerMetadata value);
}
