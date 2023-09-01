namespace DeviceTools;

/// <summary>Indicates the origin of a device ID.</summary>
/// <remarks>
/// <para>This should map to the bus enumerator that generated the device name, so its bus driver.</para>
/// <para>Relevant technologies will be added to this enum as needed.</para>
/// <para>
/// There is not a strict mapping between <see cref="DeviceIdSource"/> and <see cref="VendorIdSource"/>.
/// <see cref="VendorIdSource"/> mostly represents the ID database used, while <see cref="DeviceIdSource"/> provides information on the origin of the ID.
/// </para>
/// </remarks>
// NB: Maybe this should be named DeviceEnumerator ?
public enum DeviceIdSource : byte
{
	/// <summary>Origin of the device ID is unknown.</summary>
	Unknown = 0,
	/// <summary>Device ID coming from a PNP enumerator.</summary>
	PlugAndPlay = 1,
	/// <summary>Device ID coming from a Display enumerator.</summary>
	/// <remarks>Displays will generally use PNP IDs.</remarks>
	Display = 2,
	/// <summary>Device ID coming from a PCI enumerator.</summary>
	/// <remarks>These devices should always use PCI IDs.</remarks>
	Pci = 3,
	/// <summary>Device ID coming from a USB enumerator.</summary>
	/// <remarks>These devices should always use USB IDs.</remarks>
	Usb = 4,
	/// <summary>Device ID coming from a Bluetooth enumerator.</summary>
	/// <remarks>These devices can currently use USB or Bluetooth IDs.</remarks>
	Bluetooth = 5,
	/// <summary>Device ID coming from a Bluetooth LE enumerator.</summary>
	/// <remarks>These devices can currently use USB or Bluetooth IDs.</remarks>
	BluetoothLowEnergy = 6,
	/// <summary>Device ID from Logi HID++ eQuad.</summary>
	/// <remarks>These devices use USB IDs. The USB product ID namespace provides different </remarks>
	EQuad = 128,
}
