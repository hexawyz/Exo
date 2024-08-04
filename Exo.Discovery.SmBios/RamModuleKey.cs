using DeviceTools.Firmware;

namespace Exo.Discovery;

public readonly record struct RamModuleKey
{
	public required JedecManufacturerId ManufacturerId { get; init; }
	public required string PartNumber { get; init; }
}
