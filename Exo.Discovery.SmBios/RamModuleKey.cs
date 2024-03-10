namespace Exo.Discovery;

public readonly record struct RamModuleKey
{
	public required JedecManufacturerCode ManufacturerCode { get; init; }
	public required string PartNumber { get; init; }
}
