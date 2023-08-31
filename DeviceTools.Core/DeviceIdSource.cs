namespace DeviceTools;

/// <summary>Indicates the origin of a device ID.</summary>
/// <remarks>
/// <para>This should map to the bus enumerator that generated the device name, so its bus driver.</para>
/// <para>Relevant technologies will be added to this enum as needed.</para>
/// </remarks>
// NB: Maybe this should be named DeviceEnumerator ?
public enum DeviceIdSource : byte
{
	Unknown = 0,
	PlugAndPlay = 1,
	Pci = 2,
	Usb = 3,
	Bluetooth = 4,
	BluetoothLowEnergy = 5,
}
