namespace Exo.Devices.Razer;

internal readonly struct DeviceNotificationOptions
{
	// USB dongles can support multiple distinct notification streams, supporting the same protocol.
	// Knowing from which stream a notification comes is important for proper device management.
	public required byte StreamIndex { get; init; }
	public required byte ReportId { get; init; }
	// Seemingly, the HID report ID will be duplicated over Bluetooth, probably due to a bug in the implementation.
	// From what I understand, the various HID collections are mapped to a specific BLE GATT handle, which allows the system to map them correctly.
	// Found this, which might explain why we see this weird behavior: https://stackoverflow.com/questions/25402502/ios-ignores-input-report-of-consumer-page-of-hid-over-gatt
	// It could actually be a voluntary quirk made to support OS that don't properly HID over GATT 🙁
	public required bool HasBluetoothHidQuirk { get; init; }
	public required byte ReportLength { get; init; }
}
