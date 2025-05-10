using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class EmbeddedMonitorDeviceSettingsControl : UserControl
{
	public EmbeddedMonitorFeaturesViewModel EmbeddedMonitorFeatures
	{
		get => (EmbeddedMonitorFeaturesViewModel)GetValue(EmbeddedMonitorFeaturesProperty);
		set => SetValue(EmbeddedMonitorFeaturesProperty, value);
	}

	public static readonly DependencyProperty EmbeddedMonitorFeaturesProperty = DependencyProperty.Register
	(
		nameof(EmbeddedMonitorFeatures),
		typeof(EmbeddedMonitorFeaturesViewModel),
		typeof(EmbeddedMonitorDeviceSettingsControl),
		new PropertyMetadata(null)
	);

	public EmbeddedMonitorDeviceSettingsControl()
	{
		InitializeComponent();
	}
}
