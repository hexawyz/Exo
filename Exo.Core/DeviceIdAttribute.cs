using System;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Core;

/// <summary>Declare the vendor ID and product ID of a device supported by a driver.</summary>
/// <remarks>
/// This can optionally declare the supported version of a product. If that is the case, each supported version must be listed explicitly with an instance of <see cref="DeviceIdAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DeviceIdAttribute : Attribute
{
	public DeviceIdAttribute(VendorIdSource vendorIdSource, ushort vendorId, ushort productId)
		: this(vendorIdSource, vendorId, productId, null) { }

	public DeviceIdAttribute(VendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort? version)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
		ProductId = productId;
		Version = version;
	}

	/// <summary>The vendor ID source.</summary>
	public VendorIdSource VendorIdSource { get; }
	/// <summary>The vendor ID, uniquely identifying the device manufacturer in the corresponding database.</summary>
	public ushort VendorId { get; }
	/// <summary>The product ID, manufacturer specific.</summary>
	public ushort ProductId { get; }
	/// <summary>Optional version number to match.</summary>
	public ushort? Version { get; }
}
