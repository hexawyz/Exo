using System;
using DeviceTools;

namespace Exo;

/// <summary>Declare the vendor ID and product ID of devices supported by a driver.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProductVersionAttribute : ProductIdAttribute
{
	public ProductVersionAttribute(VendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort version)
		: base(vendorIdSource, vendorId, productId)
	{
		Version = version;
	}

	/// <summary>Version number to match.</summary>
	public ushort Version { get; }
}
