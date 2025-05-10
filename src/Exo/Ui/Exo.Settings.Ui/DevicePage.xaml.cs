using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Exo.Settings.Ui;

internal sealed partial class DevicePage : Page
{
	public SettingsViewModel SettingsViewModel { get; }
	public DevicesViewModel Devices { get; }

	public DevicePage()
	{
		SettingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
		Devices = SettingsViewModel.Devices;
		InitializeComponent();
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		var devicesViewModel = SettingsViewModel.Devices;
		var deviceId = (Guid)e.Parameter;
		var selectedDevice = SettingsViewModel.Devices.SelectedDevice;

		if (selectedDevice is null || selectedDevice.Id != deviceId)
		{
			foreach (var device in SettingsViewModel.Devices.Devices)
			{
				if (device.Id == deviceId)
				{
					selectedDevice = device;
					break;
				}
			}

			devicesViewModel.SelectedDevice = selectedDevice;
		}
	}

	protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
	{
		SettingsViewModel.Devices.SelectedDevice = null;
	}
}
