using System;
using DeviceTools;

namespace Exo;

/// <summary>Declare the vendor ID and product ID of devices supported by a driver.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ProductIdAttribute : VendorIdAttribute
{
	public ProductIdAttribute(VendorIdSource vendorIdSource, ushort vendorId, ushort productId)
		: base(vendorIdSource, vendorId)
	{
		ProductId = productId;
	}

	/// <summary>The product ID, manufacturer specific.</summary>
	public ushort ProductId { get; }
}
