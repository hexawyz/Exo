using Exo.Contracts.Ui;
using Exo.Service;

namespace Exo.Settings.Ui.Ipc;

internal interface IServiceClient
{
	void OnConnected(IServiceControl? control);
	void OnDisconnected();
	void OnMetadataSourceNotification(MetadataSourceChangeNotification notification);
	void OnMenuUpdate(MenuChangeNotification notification);
	void OnDeviceNotification(Service.WatchNotificationKind kind, DeviceStateInformation deviceInformation);
	void OnSensorDeviceUpdate(SensorDeviceInformation sensorDevice);
	void OnSensorDeviceConfigurationUpdate(SensorConfigurationUpdate sensorConfiguration);
}
