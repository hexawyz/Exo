using DeviceTools;

namespace Exo;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PnpVendorIdAttribute : Attribute
{
	public PnpVendorId VendorId { get; }

	public PnpVendorIdAttribute(string monitorName) => VendorId = PnpVendorId.Parse(monitorName);
	public PnpVendorIdAttribute(ushort vendorId) => VendorId = PnpVendorId.FromRaw(vendorId);
}
