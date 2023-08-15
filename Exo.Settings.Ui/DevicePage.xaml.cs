using System;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Exo.Settings.Ui;

public sealed partial class DevicePage : Page
{
	public DevicePage()
	{
		InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		var devicesViewModel = ViewModel.Devices;
		var deviceId = (Guid)e.Parameter;
		var selectedDevice = ViewModel.Devices.SelectedDevice;

		if (selectedDevice is null || selectedDevice.Id != deviceId)
		{
			foreach (var device in ViewModel.Devices.Devices)
			{
				if (device.Id == deviceId)
				{
					selectedDevice = device;
					break;
				}
			}

			devicesViewModel.SelectedDevice = selectedDevice;
		}

		ViewModel.Title = selectedDevice is not null ? selectedDevice.FriendlyName : "<Unknown Device>";
	}
}
