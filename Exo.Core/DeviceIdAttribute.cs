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
	public DeviceIdAttribute(VendorIdSource vendorIdSource, ushort vendorId, ushort productId, string? name)
		: this(vendorIdSource, vendorId, productId, null, null) { }

	public DeviceIdAttribute(VendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort? version, string? name)
	{
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
		ProductId = productId;
		Version = version;
		Name = name;
	}

	/// <summary>The vendor ID source.</summary>
	public VendorIdSource VendorIdSource { get; }
	/// <summary>The vendor ID, uniquely identifying the device manufacturer in the corresponding database.</summary>
	public ushort VendorId { get; }
	/// <summary>The product ID, manufacturer specific.</summary>
	public ushort ProductId { get; }
	/// <summary>Optional version number to match.</summary>
	public ushort? Version { get; }
	/// <summary>Name of the device.</summary>
	/// <remarks>
	/// <para>When possible, this should be provided with the standard name of the device associated with the VID/PID pair.</para>
	/// <para>It is possible that some VID/PID pairs do not uniquely identify a device. While that should be rare, the name should be left empty in that case.</para>
	/// <para>
	/// If the real name of a device is unknown, it should always be possible to create a generic name helping identify the the device for the users.
	/// e.g. "RGB LED Manager" or "Keyboard NNNN"
	/// </para>
	/// </remarks>
	public string? Name { get; }
}
