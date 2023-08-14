namespace Exo.Devices.Razer;

internal interface IRazerDeviceNotificationSink
{
	void OnDeviceArrival(byte deviceIndex);

	void OnDeviceRemoval(byte deviceIndex);

	void OnDeviceDpiChange(byte deviceIndex, ushort dpi1, ushort dpi2);
}
