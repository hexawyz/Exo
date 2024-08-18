using Exo.Contracts.Ui.Settings;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class PowerFeaturesViewModel : ChangeableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly IPowerService _powerService;
	private BatteryStateViewModel? _batteryState;

	public PowerFeaturesViewModel(DeviceViewModel device, IPowerService powerService)
	{
		_device = device;
		_powerService = powerService;
	}

	public override bool IsChanged { get; }

	public DeviceViewModel Device => _device;

	public BatteryStateViewModel? BatteryState
	{
		get => _batteryState;
		set => SetValue(ref _batteryState, value, ChangedProperty.BatteryState);
	}
}
