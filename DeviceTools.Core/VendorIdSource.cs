namespace DeviceTools;

/// <summary>Indicates the source of the Vendor ID.</summary>
/// <remarks>
/// This is different from BluetoothVendorIdSource, as we want to represent more values here.
/// However, it is possible to map between the two if needed.
/// </remarks>
public enum VendorIdSource : byte
{
	Unknown = 0,
	PlugAndPlay = 1,
	Pci = 2,
	Usb = 3,
	Bluetooth = 4,
}
