using System.Collections.Immutable;
using Exo.Contracts;
using Exo.Contracts.Ui;
using Exo.Service;

namespace Exo.Settings.Ui.Ipc;

internal interface IServiceClient
{
	void OnConnected(IServiceControl? control);
	void OnDisconnected();

	void OnMetadataSourceNotification(MetadataSourceChangeNotification notification);

	void OnMenuUpdate(MenuChangeNotification notification);

	void OnImageUpdate(Service.WatchNotificationKind kind, ImageInformation information);

	void OnLightingEffectUpdate(LightingEffectInformation effect);

	void OnDeviceNotification(Service.WatchNotificationKind kind, DeviceStateInformation deviceInformation);

	void OnPowerDeviceUpdate(PowerDeviceInformation powerDevice);
	void OnBatteryUpdate(BatteryChangeNotification batteryNotification);
	void OnLowPowerBatteryThresholdUpdate(Guid deviceId, Half threshold);
	void OnIdleSleepTimerUpdate(Guid deviceId, TimeSpan idleTimer);
	void OnWirelessBrightnessUpdate(Guid deviceId, byte brightness);

	void OnMouseDeviceUpdate(MouseDeviceInformation mouseDevice);
	void OnMouseDpiUpdate(Guid deviceId, byte? activeDpiPresetIndex, DotsPerInch dpi);
	void OnMouseDpiPresetsUpdate(Guid deviceId, byte? activeDpiPresetIndex, ImmutableArray<DotsPerInch> dpiPresets);
	void OnMousePollingFrequencyUpdate(Guid deviceId, ushort pollingFrequency);

	void OnLightingDeviceUpdate(LightingDeviceInformation lightingDevice);
	void OnLightingDeviceConfigurationUpdate(LightingDeviceConfiguration configuration);

	void OnMonitorDeviceUpdate(MonitorInformation monitorDevice);
	void OnMonitorSettingUpdate(MonitorSettingValue setting);

	void OnSensorDeviceUpdate(SensorDeviceInformation sensorDevice);
	void OnSensorDeviceConfigurationUpdate(SensorConfigurationUpdate sensorConfiguration);
}
