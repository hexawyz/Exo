using System;

namespace Exo.Core
{
	/// <summary>Declare the vendor ID and product ID of a device supported by a driver.</summary>
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class DeviceIdAttribute : Attribute
	{
		public DeviceIdAttribute(ushort vendorId, ushort productId)
		{
			VendorId = vendorId;
			ProductId = productId;
		}

		public ushort VendorId { get; }
		public ushort ProductId { get; }
	}
}
