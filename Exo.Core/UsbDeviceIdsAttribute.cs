using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Exo.Core
{
	public sealed class UsbDeviceIdsAttribute : Attribute
	{
		public UsbDeviceIdsAttribute(ushort vendorId, params ushort[] productIds)
		{
			VendorId = vendorId;
			ProductIds = ImmutableArray.Create(productIds);
		}

		public ushort VendorId { get; }
		public ImmutableArray<ushort> ProductIds { get; }
	}
}
