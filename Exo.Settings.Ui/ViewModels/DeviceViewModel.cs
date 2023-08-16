using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal class DeviceViewModel : BaseDeviceViewModel
{
	private readonly ExtendedDeviceInformation _extendedDeviceInformation;

	public DeviceViewModel(DeviceInformation deviceInformation, ExtendedDeviceInformation extendedDeviceInformation)
		: base(deviceInformation)
	{
		_extendedDeviceInformation = extendedDeviceInformation;
	}

	public DeviceId? DeviceId => _extendedDeviceInformation.DeviceId;

	public string? SerialNumber => _extendedDeviceInformation.SerialNumber;

	private float? _batteryLevel;

	public float? BatteryLevel
	{
		get => _batteryLevel;
		set => SetValue(ref _batteryLevel, value, ChangedProperty.BatteryLevel);
	}
}
