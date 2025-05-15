using System.Text.Json.Serialization;

namespace Exo.Service.Configuration;

[TypeId(0x91731318, 0x06F0, 0x402E, 0x8F, 0x6F, 0x23, 0x3D, 0x8C, 0x4D, 0x8F, 0xE5)]
internal record class IndexedConfigurationKey
{
	public IndexedConfigurationKey(DeviceConfigurationKey key, int instanceIndex)
		: this(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.UniqueId, instanceIndex) { }

	public IndexedConfigurationKey(IndexedConfigurationKey key, int instanceIndex)
		: this(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.UniqueId, instanceIndex) { }

	[JsonConstructor]
	public IndexedConfigurationKey(string driverKey, string deviceMainId, string compatibleHardwareId, string? uniqueId, int instanceIndex)
	{
		DriverKey = driverKey;
		DeviceMainId = deviceMainId;
		CompatibleHardwareId = compatibleHardwareId;
		UniqueId = uniqueId;
		InstanceIndex = instanceIndex;
	}

	public string DriverKey { get; }
	public string DeviceMainId { get; }
	public string CompatibleHardwareId { get; }
	public string? UniqueId { get; }
	public int InstanceIndex { get; }

	public static explicit operator DeviceConfigurationKey(IndexedConfigurationKey key)
		=> new(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.UniqueId);
}
