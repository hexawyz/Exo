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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Exo.Settings.Ui;

internal sealed partial class MonitorSettingControl : UserControl
{
	public MonitorDeviceSettingViewModel? Setting
	{
		get => (MonitorDeviceSettingViewModel)GetValue(SettingProperty);
		set => SetValue(SettingProperty, value);
	}

	public static readonly DependencyProperty SettingProperty = DependencyProperty.Register
	(
		nameof(Setting),
		typeof(MonitorDeviceSettingViewModel),
		typeof(MonitorSettingControl),
		new PropertyMetadata(null, (d, e) => ((MonitorSettingControl)d).OnMonitorFeaturesPropertyChanged((MonitorDeviceSettingViewModel)e.NewValue))
	);

	public MonitorSettingControl()
	{
		InitializeComponent();
	}

	private void OnMonitorFeaturesPropertyChanged(MonitorDeviceSettingViewModel value) => ((FrameworkElement)Content).DataContext = value;

	private void OnResetButtonClick(object sender, RoutedEventArgs e) => Setting.Reset();
}
