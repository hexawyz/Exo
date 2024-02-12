using DeviceTools;

namespace Exo.Discovery;

public readonly record struct ProductVersionKey
{
	public readonly VendorIdSource VendorIdSource { get; init; }
	public readonly ushort VendorId { get; init; }
	public readonly ushort ProductId { get; init; }
	public readonly ushort VersionNumber { get; init; }

	public ProductVersionKey(VendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort versionNumber)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
		ProductId = productId;
		VersionNumber = versionNumber;
	}
}
