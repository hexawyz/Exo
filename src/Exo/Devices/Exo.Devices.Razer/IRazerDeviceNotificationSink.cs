namespace Exo.Devices.Razer;

internal interface IRazerDeviceNotificationSink
{
	// For old receivers bound to a single device. Those do not transmit any extra data but indicate when a device became on or off.
	void OnDeviceAvailabilityChange(byte notificationStreamIndex);

	void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex);
	void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex, ushort productId);

	void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex);
	void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex, ushort productId);

	void OnDeviceDpiChange(byte notificationStreamIndex, ushort dpiX, ushort dpiY);

	void OnDeviceExternalPowerChange(byte notificationStreamIndex, bool isConnectedToExternalPower);

	void OnDeviceBatteryLevelChange(byte notificationStreamIndex, byte deviceIndex, byte batteryLevel);
}
