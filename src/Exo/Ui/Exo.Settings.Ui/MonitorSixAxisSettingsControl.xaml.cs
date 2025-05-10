using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class MonitorSixAxisSettingsControl : UserControl
{
	public MonitorDeviceFeaturesViewModel MonitorDeviceFeatures
	{
		get => (MonitorDeviceFeaturesViewModel)GetValue(MonitorDeviceFeaturesProperty);
		set => SetValue(MonitorDeviceFeaturesProperty, value);
	}

	public static readonly DependencyProperty MonitorDeviceFeaturesProperty = DependencyProperty.Register
	(
		nameof(MonitorDeviceFeatures),
		typeof(MonitorDeviceFeaturesViewModel),
		typeof(MonitorSixAxisSettingsControl),
		new PropertyMetadata(null)
	);

	public MonitorSixAxisSettingsControl() => InitializeComponent();
}
