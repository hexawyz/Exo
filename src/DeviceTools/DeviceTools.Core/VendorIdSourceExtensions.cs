namespace DeviceTools;

public static class VendorIdSourceExtensions
{
	public static bool TryGetBluetoothVendorIdSource(this VendorIdSource vendorIdSource, out BluetoothVendorIdSource bluetoothVendorIdSource)
	{
		switch (vendorIdSource)
		{
		case VendorIdSource.Usb:
			bluetoothVendorIdSource = BluetoothVendorIdSource.Usb;
			return true;
		case VendorIdSource.Bluetooth:
			bluetoothVendorIdSource = BluetoothVendorIdSource.Usb;
			return true;
		default:
			bluetoothVendorIdSource = default;
			return false;
		}
	}

	public static VendorIdSource AsVendorIdSource(this BluetoothVendorIdSource vendorIdSource) =>
		vendorIdSource switch
		{
			BluetoothVendorIdSource.Bluetooth => VendorIdSource.Bluetooth,
			BluetoothVendorIdSource.Usb => VendorIdSource.Usb,
			_ => throw new InvalidOperationException("Unsupported value.")
		};
}
