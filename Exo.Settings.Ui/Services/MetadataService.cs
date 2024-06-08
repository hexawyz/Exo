using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exo.Archive;

namespace Exo.Settings.Ui.Services;

internal sealed class MetadataService : IMetadataService, IDisposable
{
	private readonly StringMetadataResolver _stringMetadataResolver;
	private readonly DeviceMetadataResolver<LightingEffectMetadata> _lightingEffectMetadataResolver;
	private readonly DeviceMetadataResolver<LightingZoneMetadata> _lightingZoneMetadataResolver;
	private readonly DeviceMetadataResolver<SensorMetadata> _sensorMetadataResolver;
	private readonly DeviceMetadataResolver<CoolerMetadata> _coolerMetadataResolver;

	public MetadataService
	(
		StringMetadataResolver stringMetadataResolver,
		DeviceMetadataResolver<LightingEffectMetadata> lightingEffectMetadataResolver,
		DeviceMetadataResolver<LightingZoneMetadata> lightingZoneMetadataResolver,
		DeviceMetadataResolver<SensorMetadata> sensorMetadataResolver,
		DeviceMetadataResolver<CoolerMetadata> coolerMetadataResolver
	)
	{
		_stringMetadataResolver = stringMetadataResolver;
		_lightingEffectMetadataResolver = lightingEffectMetadataResolver;
		_lightingZoneMetadataResolver = lightingZoneMetadataResolver;
		_sensorMetadataResolver = sensorMetadataResolver;
		_coolerMetadataResolver = coolerMetadataResolver;
	}

	public void Dispose()
	{
		_stringMetadataResolver.Dispose();
		_lightingEffectMetadataResolver.Dispose();
		_lightingZoneMetadataResolver.Dispose();
		_sensorMetadataResolver.Dispose();
		_coolerMetadataResolver.Dispose();
	}

	public string? GetStringAsync(CultureInfo? culture, Guid stringId)
		=> _stringMetadataResolver.GetStringAsync(culture, stringId);

	public bool TryGetLightingEffectMetadata(string driverKey, string compatibleId, Guid lightingZoneId, out LightingEffectMetadata value)
		=> _lightingEffectMetadataResolver.TryGetData(driverKey, compatibleId, lightingZoneId, out value);

	public bool TryGetLightingZoneMetadata(string driverKey, string compatibleId, Guid lightingZoneId, out LightingZoneMetadata value)
		=> _lightingZoneMetadataResolver.TryGetData(driverKey, compatibleId, lightingZoneId, out value);

	public bool TryGetSensorMetadataAsync(string driverKey, string compatibleId, Guid sensorId, out SensorMetadata value)
		=> _sensorMetadataResolver.TryGetData(driverKey, compatibleId, sensorId, out value);

	public bool TryGetCoolerMetadataAsync(string driverKey, string compatibleId, Guid coolerId, out CoolerMetadata value)
		=> _coolerMetadataResolver.TryGetData(driverKey, compatibleId, coolerId, out value);
}
