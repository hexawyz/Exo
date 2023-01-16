using DeviceTools;

namespace Exo.Service;

public record struct HidVersionedProductKey
{
	public readonly VendorIdSource VendorIdSource { get; init; }
	public readonly ushort VendorId { get; init; }
	public readonly ushort ProductId { get; init; }
	public readonly ushort VersionNumber { get; init; }

	public HidVersionedProductKey(VendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort versionNumber)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
		ProductId = productId;
		VersionNumber = versionNumber;
	}
}
