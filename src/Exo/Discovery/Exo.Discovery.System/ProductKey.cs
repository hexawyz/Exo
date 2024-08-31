using DeviceTools;

namespace Exo.Discovery;

public readonly record struct ProductKey
{
	public readonly VendorIdSource VendorIdSource { get; init; }
	public readonly ushort VendorId { get; init; }
	public readonly ushort ProductId { get; init; }

	public ProductKey(VendorIdSource vendorIdSource, ushort vendorId, ushort productId)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
		ProductId = productId;
	}
}
