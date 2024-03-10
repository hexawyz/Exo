namespace Exo.Discovery;

public readonly struct MemoryModuleInformation
{
	public byte Index { get; init; }
	public JedecManufacturerCode ManufacturerCode { get; init; }
	public string PartNumber { get; init; }
}
