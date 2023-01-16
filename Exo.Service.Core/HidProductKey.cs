using DeviceTools;

namespace Exo.Service;

public record struct HidProductKey
{
	public readonly VendorIdSource VendorIdSource { get; init; }
	public readonly ushort VendorId { get; init; }
	public readonly ushort ProductId { get; init; }

	public HidProductKey(VendorIdSource vendorIdSource, ushort vendorId, ushort productId)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
		ProductId = productId;
	}
}
