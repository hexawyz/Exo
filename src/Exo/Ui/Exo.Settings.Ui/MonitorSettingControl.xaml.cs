using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
}
