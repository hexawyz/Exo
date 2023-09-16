using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal class DeviceViewModel : BaseDeviceViewModel
{
	private readonly ExtendedDeviceInformation _extendedDeviceInformation;

	public DeviceViewModel(DeviceInformation deviceInformation, ExtendedDeviceInformation extendedDeviceInformation)
		: base(deviceInformation)
	{
		_extendedDeviceInformation = extendedDeviceInformation;
		if (deviceInformation.FeatureIds is not null && deviceInformation.FeatureIds.Contains(WellKnownGuids.MouseDeviceFeature))
		{
			MouseFeatures = new(this);
		}
	}

	public DeviceId? DeviceId => _extendedDeviceInformation.DeviceId;

	public string? SerialNumber => _extendedDeviceInformation.SerialNumber;

	private BatteryStateViewModel? _batteryState;
	public BatteryStateViewModel? BatteryState
	{
		get => _batteryState;
		set => SetValue(ref _batteryState, value, ChangedProperty.BatteryState);
	}

	// If the device is a mouse, this hosts the mouse-related features.
	public MouseDeviceFeaturesViewModel? MouseFeatures { get; }
}
