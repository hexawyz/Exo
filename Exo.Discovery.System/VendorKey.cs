using DeviceTools;

namespace Exo.Discovery;

public readonly record struct VendorKey
{
	public readonly VendorIdSource VendorIdSource { get; init; }
	public readonly ushort VendorId { get; init; }

	public VendorKey(VendorIdSource vendorIdSource, ushort vendorId)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
	}
}
