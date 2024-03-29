using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Runtime.Serialization;
using static System.Net.Mime.MediaTypeNames;

namespace DeviceTools;

/// <summary>Represents detailed information on a device ID.</summary>
/// <remarks>
/// This structure contains information that can be used to uniquely identify hardware.
/// It does, however, not identify any specific instance of the hardware.
/// </remarks>
[DataContract]
public readonly struct DeviceId : IEquatable<DeviceId>
{
	/// <summary>A value to use to represent an invalid device ID.</summary>
	public static readonly DeviceId Invalid = new DeviceId(DeviceIdSource.Unknown, VendorIdSource.Unknown, 0xFFFF, 0xFFFF, 0xFFFF);

	/// <summary>Creates a device ID for a monitor device.</summary>
	/// <param name="vendorId">The PNP vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForDisplay(string vendorId, ushort productId)
		=> new(DeviceIdSource.Display, VendorIdSource.PlugAndPlay, PnpVendorId.Parse(vendorId).Value, productId, 0xFFFF);

	/// <summary>Creates a device ID for a monitor device.</summary>
	/// <param name="vendorId">The PNP vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForDisplay(PnpVendorId vendorId, ushort productId)
		=> new(DeviceIdSource.Display, VendorIdSource.PlugAndPlay, vendorId.Value, productId, 0xFFFF);

	/// <summary>Creates a device ID for a monitor device.</summary>
	/// <param name="vendorId">The PNP vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForDisplay(ReadOnlySpan<char> vendorId, ushort productId)
		=> new(DeviceIdSource.Display, VendorIdSource.PlugAndPlay, PnpVendorId.Parse(vendorId).Value, productId, 0xFFFF);

	/// <summary>Creates a device ID for a monitor device.</summary>
	/// <param name="vendorId">The PNP vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForDisplay(ReadOnlySpan<byte> vendorId, ushort productId)
		=> new(DeviceIdSource.Display, VendorIdSource.PlugAndPlay, PnpVendorId.Parse(vendorId).Value, productId, 0xFFFF);

	/// <summary>Creates a device ID for a PCI device.</summary>
	/// <param name="vendorId">The PCI vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForPci(ushort vendorId, ushort productId)
		=> new(DeviceIdSource.Pci, VendorIdSource.Pci, vendorId, productId, 0xFFFF);

	/// <summary>Creates a device ID for a PCI device.</summary>
	/// <param name="vendorId">The PCI vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <param name="version">The version of the device.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForPci(ushort vendorId, ushort productId, byte version)
		=> new(DeviceIdSource.Pci, VendorIdSource.Pci, vendorId, productId, version);

	/// <summary>Creates a device ID for an USB device.</summary>
	/// <param name="vendorId">The USB vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <param name="version">The version of the device.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForUsb(ushort vendorId, ushort productId, ushort version)
		=> new(DeviceIdSource.Usb, VendorIdSource.Usb, vendorId, productId, version);

	/// <summary>Creates a device ID for a BT device.</summary>
	/// <remarks>Information for BT and BLE devices is functionally identical, however, the format of device names is slightly different.</remarks>
	/// <param name="vendorIdSource">The device ID source.</param>
	/// <param name="vendorId">The vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <param name="version">The version of the device.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForBluetooth(BluetoothVendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort version)
		=> new(DeviceIdSource.Bluetooth, vendorIdSource.AsVendorIdSource(), vendorId, productId, version);

	/// <summary>Creates a device ID for a BLE device.</summary>
	/// <remarks>Information for BT and BLE devices is functionally identical, however, the format of device names is slightly different.</remarks>
	/// <param name="vendorIdSource">The device ID source.</param>
	/// <param name="vendorId">The vendor ID.</param>
	/// <param name="productId">The product ID.</param>
	/// <param name="version">The version of the device.</param>
	/// <returns>A device ID.</returns>
	public static DeviceId ForBluetoothLowEnergy(BluetoothVendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort version)
		=> new(DeviceIdSource.BluetoothLowEnergy, vendorIdSource.AsVendorIdSource(), vendorId, productId, version);

	/// <summary>Indicates the source technology of this device ID.</summary>
	/// <remarks>
	/// <para>While not strictly part of the device ID, it is (can be) part of the device name and it is important information to properly interpret the device ID.</para>
	/// <para>
	/// Please note that the reliability of this information cannot be strictly guaranteed, as it largely depends on specific knowledge about the various device enumerators (bus drivers),
	/// and new technologies and specific cases will always arise at some point.
	/// However, we'll make sure to support what is needed to recognize device names generated by relevant device enumerators.
	/// </para>
	/// <para>At the time of writing, only USB, Bluetooth, and Bluetooth Low Energy are supported.</para>
	/// </remarks>
	[DataMember]
	public DeviceIdSource Source { get; }

	/// <summary>Indicates which ID source references the Vendor ID.</summary>
	/// <remarks>
	/// <para>
	/// Different technology associations manage or provide their own ID lists, which usually follow the same 16 bit VID & PID scheme, but are entirely different, potentially conflicting, namespaces.
	/// In the case of at least Bluetooth, they allow the published Vendor ID to originate from another database (only USB at the time of writing this comment), so it is important to know this information.
	/// </para>
	/// <para>The most notable sources for Vendor IDs would be PCI, USB and Bluetooth.</para>
	/// </remarks>
	[DataMember]
	public VendorIdSource VendorIdSource { get; }

	// TODO: Find space to support ACPI IDs which are represented as 4 letters or digits. Less than one extra byte is needed, but the current struct has a nice size of 8 bytes which would be nice to keep.
	// It is probably possible to compress the Source and VendorIdSource enums together.

	/// <summary>A number representing the Vendor ID in a technology-specific ID namespace.</summary>
	[DataMember]
	public ushort VendorId { get; }

	/// <summary>A number representing the Product ID in a Vendor-specific and technology-specific namespace.</summary>
	[DataMember]
	public ushort ProductId { get; }

	/// <summary>The version of the product, if specified.</summary>
	/// <remarks>
	/// <para>This information should usually be present in the case of USB, BT or BLE.</para>
	/// <para>An invalid version will be represented by the value <c>0xFFFF</c>.</para>
	/// </remarks>
	[DataMember]
	public ushort Version { get; }

#if NET5_0_OR_GREATER
	[System.Text.Json.Serialization.JsonConstructor]
#endif
	public DeviceId(DeviceIdSource source, VendorIdSource vendorIdSource, ushort vendorId, ushort productId, ushort version)
	{
		Source = source;
		VendorIdSource = vendorIdSource;
		VendorId = vendorId;
		ProductId = productId;
		Version = version;
	}

	public override bool Equals(object? obj) => obj is DeviceId id && Equals(id);

	public bool Equals(DeviceId other)
		=> Source == other.Source &&
			VendorIdSource == other.VendorIdSource &&
			VendorId == other.VendorId &&
			ProductId == other.ProductId &&
			Version == other.Version;

#if NETSTANDARD2_0
	public override int GetHashCode()
	{
		int hashCode = 1678542449;
		hashCode = hashCode * -1521134295 + Source.GetHashCode();
		hashCode = hashCode * -1521134295 + VendorIdSource.GetHashCode();
		hashCode = hashCode * -1521134295 + VendorId.GetHashCode();
		hashCode = hashCode * -1521134295 + ProductId.GetHashCode();
		hashCode = hashCode * -1521134295 + Version.GetHashCode();
		return hashCode;
	}
#else
	public override int GetHashCode() => HashCode.Combine(Source, VendorIdSource, VendorId, ProductId, Version);
#endif

	public static bool operator ==(DeviceId left, DeviceId right) => left.Equals(right);
	public static bool operator !=(DeviceId left, DeviceId right) => !(left == right);

	public override string ToString()
		=> VendorIdSource switch
		{
			VendorIdSource.PlugAndPlay => $"{PnpVendorId.FromRaw(VendorId)}{ProductId:X4}",
			_ => $"{VendorId:X4}:{ProductId:X4}",
		};
}
