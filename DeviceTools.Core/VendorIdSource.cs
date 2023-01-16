namespace DeviceTools;

/// <summary>Indicates the source of the Vendor ID.</summary>
/// <remarks>
/// This is different from BluetoothVendorIdSource, as we want to represent more values here.
/// However, it is possible to map between the two if needed.
/// </remarks>
public enum VendorIdSource : byte
{
	Unknown = 0,
	Pci = 1,
	Usb = 2,
	Bluetooth = 3,
}
