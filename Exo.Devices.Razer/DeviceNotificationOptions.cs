namespace Exo.Devices.Razer;

internal readonly struct DeviceNotificationOptions
{
	// Seemingly, the HID report ID will be duplicated over bluetooth, probably due to a bug in the implementation.
	// From what I understand, the various HID collections are mapped to a specific BLE GATT handle, which allows the system to map them correctly.
	// However the hardware developers might have made the mistake of explicitly including the report ID in the data packed as it would be done over regular USB.
	// While I'm not entirely sure about this, I can see something for what would be report ID 04 than I see for report ID 05. (No idea what 04 is about, though, I only have one empty message by luck)
	// Found this, which might explain why we see this weird behavior: https://stackoverflow.com/questions/25402502/ios-ignores-input-report-of-consumer-page-of-hid-over-gatt
	// It could actually be a voluntary quirk made to support OS that don't properly HID over GATT 🙁
	public bool HasBluetoothHidQuirk { get; init; }
	public byte ReportId { get; init; }
	public byte ReportLength { get; init; }
}
