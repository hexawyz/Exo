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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Exo.Settings.Ui;

internal sealed partial class MonitorSettingsControl : UserControl
{
	public MonitorDeviceFeaturesViewModel? MonitorFeatures
	{
		get => (MonitorDeviceFeaturesViewModel)GetValue(MonitorFeaturesProperty);
		set => SetValue(MonitorFeaturesProperty, value);
	}

	public static readonly DependencyProperty MonitorFeaturesProperty = DependencyProperty.Register
	(
		nameof(MonitorFeatures),
		typeof(MonitorDeviceFeaturesViewModel),
		typeof(MonitorSettingsControl),
		new PropertyMetadata(null, (d, e) => ((MonitorSettingsControl)d).OnMonitorFeaturesPropertyChanged((MonitorDeviceFeaturesViewModel)e.NewValue))
	);

	public MonitorSettingsControl()
	{
		InitializeComponent();
	}

	private void OnMonitorFeaturesPropertyChanged(MonitorDeviceFeaturesViewModel value) => ((FrameworkElement)Content).DataContext = value;
}
