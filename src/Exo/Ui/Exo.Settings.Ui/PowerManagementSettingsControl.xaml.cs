using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class PowerManagementSettingsControl : UserControl
{
	public PowerFeaturesViewModel? PowerFeatures
	{
		get => (PowerFeaturesViewModel)GetValue(PowerFeaturesProperty);
		set => SetValue(PowerFeaturesProperty, value);
	}

	public static readonly DependencyProperty PowerFeaturesProperty = DependencyProperty.Register
	(
		nameof(PowerFeatures),
		typeof(PowerFeaturesViewModel),
		typeof(PowerManagementSettingsControl),
		new PropertyMetadata(null)
	);

	public PowerManagementSettingsControl()
	{
		InitializeComponent();
	}
}
