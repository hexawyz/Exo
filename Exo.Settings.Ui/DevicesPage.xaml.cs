using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class DevicesPage : Page
{
	public DevicesPage()
	{
		InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

	private void OnDeviceButtonClick(object sender, RoutedEventArgs e)
	{
		var device = (DeviceViewModel)((FrameworkElement)sender).DataContext;
		Frame.Navigate(typeof(DevicePage), device.Id);
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		ViewModel.Icon = "\uE772";
		ViewModel.Title = "Devices";
	}
}
