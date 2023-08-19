using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal class MouseDeviceFeaturesViewModel : BindableObject
{
	private readonly DeviceViewModel _device;

	public MouseDeviceFeaturesViewModel(DeviceViewModel device) => _device = device;

	private DpiViewModel? _currentDpi;
	public DpiViewModel? CurrentDpi
	{
		get => _currentDpi;
		set => SetValue(ref _currentDpi, value);
	}
}
