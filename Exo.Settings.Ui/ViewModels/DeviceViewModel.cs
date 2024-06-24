using System.Windows.Input;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;

namespace Exo.Settings.Ui.ViewModels;

internal class DeviceViewModel : BaseDeviceViewModel
{
	//private readonly ExtendedDeviceInformation _extendedDeviceInformation;

	public DeviceViewModel(SettingsServiceConnectionManager connectionManager, ISettingsMetadataService metadataService, DeviceInformation deviceInformation)
		: base(deviceInformation)
	{
		//_extendedDeviceInformation = extendedDeviceInformation;
		if (deviceInformation.FeatureIds is not null)
		{
			if (deviceInformation.FeatureIds.Contains(WellKnownGuids.MouseDeviceFeature))
			{
				MouseFeatures = new(this);
			}
			if (deviceInformation.FeatureIds.Contains(WellKnownGuids.MonitorDeviceFeature))
			{
				MonitorFeatures = new(this, metadataService, connectionManager);
			}
		}
	}

	private BatteryStateViewModel? _batteryState;
	public BatteryStateViewModel? BatteryState
	{
		get => _batteryState;
		set => SetValue(ref _batteryState, value, ChangedProperty.BatteryState);
	}

	// If the device is a mouse, this hosts the mouse-related features.
	public MouseDeviceFeaturesViewModel? MouseFeatures { get; }

	// If the device is a monitor, this hosts the mouse-related features.
	public MonitorDeviceFeaturesViewModel? MonitorFeatures { get; }
}
