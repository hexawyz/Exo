using DeviceTools.Firmware;

namespace Exo.Discovery;

public readonly struct MemoryModuleInformation
{
	public byte Index { get; init; }
	public JedecManufacturerId ManufacturerId { get; init; }
	public string PartNumber { get; init; }
}
