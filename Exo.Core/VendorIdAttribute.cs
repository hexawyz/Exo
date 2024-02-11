using System;
using DeviceTools;

namespace Exo;

/// <summary>Declare the vendor ID of devices supported by a driver.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class VendorIdAttribute : Attribute
{
	public VendorIdAttribute(VendorIdSource vendorIdSource, ushort vendorId)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
	}

	/// <summary>The vendor ID source.</summary>
	public VendorIdSource VendorIdSource { get; }
	/// <summary>The vendor ID, uniquely identifying the device manufacturer in the corresponding database.</summary>
	public ushort VendorId { get; }
}
