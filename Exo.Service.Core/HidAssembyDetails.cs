using System.Collections.Generic;

namespace Exo.Service;

public readonly struct HidAssembyDetails
{
	public readonly Dictionary<string, HidVendorKey[]> VendorDrivers { get; }
	public readonly Dictionary<string, HidProductKey[]> ProductDrivers { get; }
	public readonly Dictionary<string, HidVersionedProductKey[]> VersionedProductDrivers { get; }

	public HidAssembyDetails(Dictionary<string, HidVendorKey[]> vendorDrivers, Dictionary<string, HidProductKey[]> productDrivers, Dictionary<string, HidVersionedProductKey[]> productVersionDrivers)
	{
		VendorDrivers = vendorDrivers;
		ProductDrivers = productDrivers;
		VersionedProductDrivers = productVersionDrivers;
	}
}
