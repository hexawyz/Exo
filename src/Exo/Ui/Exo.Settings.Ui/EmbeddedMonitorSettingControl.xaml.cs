using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class EmbeddedMonitorSettingControl : UserControl
{
	public EmbeddedMonitorViewModel? Monitor
	{
		get => (EmbeddedMonitorViewModel)GetValue(MonitorProperty);
		set => SetValue(MonitorProperty, value);
	}

	public static readonly DependencyProperty MonitorProperty = DependencyProperty.Register
	(
		nameof(Monitor),
		typeof(EmbeddedMonitorViewModel),
		typeof(EmbeddedMonitorSettingControl),
		new PropertyMetadata(null)
	);

	public EmbeddedMonitorSettingControl()
	{
		InitializeComponent();
	}
}
