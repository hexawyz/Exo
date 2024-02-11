using DeviceTools;

namespace Exo.Discovery;

internal readonly record struct HidVendorKey
{
	public readonly VendorIdSource VendorIdSource { get; init; }
	public readonly ushort VendorId { get; init; }

	public HidVendorKey(VendorIdSource vendorIdSource, ushort vendorId)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
	}
}
