using Exo.Contracts;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Service;
using Exo.Settings.Ui.Ipc;
using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;

namespace Exo.Settings.Ui;

// The design here is stupidly straightforward.
// The "service client" has knowledge of all dependencies and explicitly dispatch them as appropriate.
// Doing things that way, we avoid most of the asynchronous synchronization stuff in UI code that is not really multithreaded.
// And at the same time, we will have a strong guarantee that connects and disconnects are properly synchronized.
internal class ExoServiceClient : IServiceClient
{
	private readonly MetadataService _metadataService;
	private readonly SettingsViewModel _settingsViewModel;

	public ExoServiceClient(MetadataService metadataService, SettingsViewModel settingsViewModel)
	{
		_metadataService = metadataService;
		_settingsViewModel = settingsViewModel;
	}

	void IServiceClient.OnConnected(IServiceControl? control)
	{
		if (control is not null)
		{
			_settingsViewModel.Devices.OnConnected(control);
			_settingsViewModel.Sensors.OnConnected(control);
			_settingsViewModel.Lighting.OnConnected(control);
		}
	}

	void IServiceClient.OnDisconnected()
	{
		_metadataService.Reset();
		_settingsViewModel.Devices.Reset();
		_settingsViewModel.Sensors.Reset();
		_settingsViewModel.Lighting.Reset();
	}

	void IServiceClient.OnDeviceNotification(Service.WatchNotificationKind kind, DeviceStateInformation deviceInformation)
	{
		_settingsViewModel.Devices.HandleDeviceNotification(kind, deviceInformation);
	}

	void IServiceClient.OnMetadataSourceNotification(MetadataSourceChangeNotification notification)
	{
		_metadataService.HandleMetadataSourceNotification(notification);
	}

	void IServiceClient.OnMenuUpdate(MenuChangeNotification notification)
	{
		// TODO
		// We actually receive the notifications but the other side of the protocol still needs to be implemented.
	}

	void IServiceClient.OnMonitorDeviceUpdate(MonitorInformation monitorDevice)
	{
		_settingsViewModel.Devices.HandleMonitorDeviceUpdate(monitorDevice);
	}

	void IServiceClient.OnMonitorSettingUpdate(MonitorSettingValue setting)
	{
		_settingsViewModel.Devices.HandleMonitorSettingUpdate(setting);
	}

	void IServiceClient.OnSensorDeviceUpdate(SensorDeviceInformation sensorDevice)
	{
		_settingsViewModel.Sensors.HandleSensorDeviceUpdate(sensorDevice);
	}

	void IServiceClient.OnSensorDeviceConfigurationUpdate(SensorConfigurationUpdate sensorConfiguration)
	{
		_settingsViewModel.Sensors.HandleSensorConfigurationUpdate(sensorConfiguration);
	}

	void IServiceClient.OnLightingEffectUpdate(LightingEffectInformation effect)
	{
		_settingsViewModel.Lighting.CacheEffectInformation(effect);
	}

	void IServiceClient.OnLightingDeviceUpdate(LightingDeviceInformation lightingDevice)
	{
		_settingsViewModel.Lighting.OnLightingDevice(lightingDevice);
	}

	void IServiceClient.OnLightingDeviceConfigurationUpdate(LightingDeviceConfiguration configuration)
	{
		_settingsViewModel.Lighting.OnLightingConfigurationUpdate(configuration);
	}
}
