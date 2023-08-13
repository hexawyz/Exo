namespace Exo.Devices.Razer;

internal interface IRazerDeviceNotificationSink
{
	void OnDeviceArrival();

	void OnDeviceRemoval();
}
