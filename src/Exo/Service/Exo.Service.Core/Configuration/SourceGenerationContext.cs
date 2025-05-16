using System.Collections.Immutable;
using System.Text.Json.Serialization;
using DeviceTools;
using Exo.Cooling;
using Exo.Cooling.Configuration;
using Exo.Images;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Exo.Monitors;

namespace Exo.Service.Configuration;

[JsonSourceGenerationOptions(
	WriteIndented = false,
	NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConfigurationVersionDetails))]
[JsonSerializable(typeof(UsedAssemblyDetails))]
[JsonSerializable(typeof(MenuConfiguration))]
[JsonSerializable(typeof(DeviceInformation))]
[JsonSerializable(typeof(IndexedConfigurationKey))]
[JsonSerializable(typeof(DeviceUserConfiguration))]
[JsonSerializable(typeof(KeyValuePair<UInt128, ImageMetadata>))]
[JsonSerializable(typeof(PersistedLightingDeviceInformation))]
[JsonSerializable(typeof(PersistedLightingZoneInformation))]
[JsonSerializable(typeof(PersistedLightingConfiguration))]
[JsonSerializable(typeof(PersistedLightingDeviceConfiguration))]
[JsonSerializable(typeof(LightingEffect))]
[JsonSerializable(typeof(PersistedLightingEffectInformation))]
[JsonSerializable(typeof(ConfigurablePropertyInformation))]
[JsonSerializable(typeof(LightingDataType))]
[JsonSerializable(typeof(EffectDirection1D))]
[JsonSerializable(typeof(ImmutableArray<EnumerationValue>))]
[JsonSerializable(typeof(EnumerationValue))]
[JsonSerializable(typeof(PersistedLightDeviceInformation))]
[JsonSerializable(typeof(PersistedLightInformation))]
[JsonSerializable(typeof(PersistedMouseInformation))]
[JsonSerializable(typeof(PersistedPowerDeviceInformation))]
[JsonSerializable(typeof(SensorDataType))]
[JsonSerializable(typeof(SensorCapabilities))]
[JsonSerializable(typeof(PersistedSensorInformation))]
[JsonSerializable(typeof(PersistedSensorConfiguration))]
[JsonSerializable(typeof(PersistedCoolerInformation))]
[JsonSerializable(typeof(CoolerConfiguration))]
[JsonSerializable(typeof(PersistedEmbeddedMonitorInformation))]
[JsonSerializable(typeof(PersistedMonitorConfiguration))]
[JsonSerializable(typeof(DiscoveredAssemblyDetails))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
