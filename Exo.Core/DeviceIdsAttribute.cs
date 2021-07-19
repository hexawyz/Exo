using System;
using System.Collections.Immutable;

namespace Exo.Core
{
	/// <summary>Similar to <see cref="DeviceIdAttribute"/> but allows to declare many devices for a same vendor.</summary>
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class DeviceIdsAttribute : Attribute
	{
		public DeviceIdsAttribute(ushort vendorId, params ushort[] productIds)
		{
			VendorId = vendorId;
			ProductIds = ImmutableArray.Create(productIds);
		}

		public ushort VendorId { get; }
		public ImmutableArray<ushort> ProductIds { get; }
	}
}
