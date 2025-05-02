using System.Collections.Immutable;
using Exo.Lighting;
using Exo.Programming;
using Exo.Service;
using Exo.Service.Ipc;
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
			_settingsViewModel.Images.OnConnected(control);
			_settingsViewModel.Devices.OnConnected(control, control, control, control, control);
			_settingsViewModel.Sensors.OnConnected(control);
			_settingsViewModel.Cooling.OnConnected(control);
			_settingsViewModel.Lighting.OnConnected(control);
			_settingsViewModel.CustomMenu.OnConnected(control);
			_settingsViewModel.OnConnected(true);
		}
		else
		{
			_settingsViewModel.OnConnected(false);
		}
	}

	void IServiceClient.OnDisconnected()
	{
		_metadataService.OnConnectionReset();
		_settingsViewModel.Images.OnConnectionReset();
		_settingsViewModel.Devices.OnConnectionReset();
		_settingsViewModel.Sensors.OnConnectionReset();
		_settingsViewModel.Cooling.OnConnectionReset();
		_settingsViewModel.Lighting.OnConnectionReset();
		_settingsViewModel.CustomMenu.OnConnectionReset();
	}

	void IServiceClient.OnDeviceNotification(Service.WatchNotificationKind kind, DeviceStateInformation deviceInformation)
		=> _settingsViewModel.Devices.HandleDeviceNotification(kind, deviceInformation);

	void IServiceClient.OnMetadataSourceNotification(MetadataSourceChangeNotification notification)
		=> _metadataService.HandleMetadataSourceNotification(notification);

	void IServiceClient.OnMenuUpdate(MenuChangeNotification notification)
		=> _settingsViewModel.CustomMenu.HandleMenuUpdate(notification);

	void IServiceClient.OnProgrammingMetadata(ImmutableArray<ModuleDefinition> modules)
		=> _settingsViewModel.Programming.HandleMetadata(modules);

	void IServiceClient.OnImageUpdate(Service.WatchNotificationKind kind, ImageInformation information)
		=> _settingsViewModel.Images.OnImageUpdate(kind, information);

	void IServiceClient.OnPowerDeviceUpdate(PowerDeviceInformation powerDevice)
		=> _settingsViewModel.Devices.HandlePowerDeviceUpdate(powerDevice);

	void IServiceClient.OnBatteryUpdate(BatteryChangeNotification batteryNotification)
		=> _settingsViewModel.Devices.HandleBatteryUpdate(batteryNotification);

	void IServiceClient.OnLowPowerBatteryThresholdUpdate(Guid deviceId, Half threshold)
		=> _settingsViewModel.Devices.HandleLowPowerModeBatteryThresholdUpdate(deviceId, threshold);

	void IServiceClient.OnIdleSleepTimerUpdate(Guid deviceId, TimeSpan idleTimer)
		=> _settingsViewModel.Devices.HandleIdleSleepTimerUpdate(deviceId, idleTimer);

	void IServiceClient.OnWirelessBrightnessUpdate(Guid deviceId, byte brightness)
		=> _settingsViewModel.Devices.HandleWirelessBrightnessUpdate(deviceId, brightness);

	void IServiceClient.OnMouseDeviceUpdate(MouseDeviceInformation mouseDevice)
		=> _settingsViewModel.Devices.HandleMouseDeviceUpdate(mouseDevice);

	void IServiceClient.OnMouseDpiUpdate(Guid deviceId, byte? activeDpiPresetIndex, DotsPerInch dpi)
		=> _settingsViewModel.Devices.HandleMouseDpiUpdate(deviceId, activeDpiPresetIndex, dpi);

	void IServiceClient.OnMouseDpiPresetsUpdate(Guid deviceId, byte? activeDpiPresetIndex, ImmutableArray<DotsPerInch> dpiPresets)
		=> _settingsViewModel.Devices.HandleMouseDpiPresetsUpdate(deviceId, activeDpiPresetIndex, dpiPresets);

	void IServiceClient.OnMousePollingFrequencyUpdate(Guid deviceId, ushort pollingFrequency)
		=> _settingsViewModel.Devices.HandleMousePollingFrequencyUpdate(deviceId, pollingFrequency);

	void IServiceClient.OnMonitorDeviceUpdate(MonitorInformation monitorDevice)
		=> _settingsViewModel.Devices.HandleMonitorDeviceUpdate(monitorDevice);

	void IServiceClient.OnMonitorSettingUpdate(MonitorSettingValue setting)
		=> _settingsViewModel.Devices.HandleMonitorSettingUpdate(setting);

	void IServiceClient.OnSensorDeviceUpdate(SensorDeviceInformation sensorDevice)
		=> _settingsViewModel.Sensors.HandleSensorDeviceUpdate(sensorDevice);

	void IServiceClient.OnSensorDeviceConfigurationUpdate(SensorConfigurationUpdate sensorConfiguration)
		=> _settingsViewModel.Sensors.HandleSensorConfigurationUpdate(sensorConfiguration);

	void IServiceClient.OnLightingEffectUpdate(LightingEffectInformation effect)
		=> _settingsViewModel.Lighting.CacheEffectInformation(effect);

	void IServiceClient.OnLightingDeviceUpdate(LightingDeviceInformation lightingDevice)
		=> _settingsViewModel.Lighting.OnLightingDevice(lightingDevice);

	void IServiceClient.OnLightingDeviceConfigurationUpdate(LightingDeviceConfiguration configuration)
		=> _settingsViewModel.Lighting.OnLightingConfigurationUpdate(configuration);

	void IServiceClient.OnEmbeddedMonitorDeviceUpdate(EmbeddedMonitorDeviceInformation embeddedMonitorDevice)
		=> _settingsViewModel.Devices.HandleEmbeddedMonitorDeviceUpdate(embeddedMonitorDevice);

	void IServiceClient.OnEmbeddedMonitorConfigurationUpdate(EmbeddedMonitorConfiguration configuration)
		=> _settingsViewModel.Devices.HandleEmbeddedMonitorConfigurationUpdate(configuration);

	void IServiceClient.OnLightDeviceUpdate(LightDeviceInformation lightDevice)
		=> _settingsViewModel.Devices.HandleLightDeviceUpdate(lightDevice);

	void IServiceClient.OnLightConfigurationUpdate(LightChangeNotification notification)
		=> _settingsViewModel.Devices.HandleLightConfigurationUpdate(notification);

	void IServiceClient.OnCoolingDeviceUpdate(CoolingDeviceInformation coolingDevice)
		=> _settingsViewModel.Cooling.HandleCoolingDeviceUpdate(coolingDevice);

	void IServiceClient.OnCoolerConfigurationUpdate(CoolingUpdate configuration)
		=> _settingsViewModel.Cooling.HandleCoolerConfigurationUpdate(configuration);
}
