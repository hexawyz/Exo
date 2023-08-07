using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Controls;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class LightingZoneControl : UserControl
{
	public LightingZoneViewModel? LightingZone
	{
		get { return (LightingZoneViewModel)GetValue(LightingZoneProperty); }
		set { SetValue(LightingZoneProperty, value); }
	}

	public static readonly DependencyProperty LightingZoneProperty = DependencyProperty.Register
	(
		nameof(LightingZone),
		typeof(LightingZoneViewModel),
		typeof(LightingZoneControl),
		new PropertyMetadata(null, (d, e) => ((LightingZoneControl)d).OnLightingZonePropertyChanged((LightingZoneViewModel)e.NewValue))
	);

	public LightingZoneControl()
	{
		InitializeComponent();
	}

	private void OnLightingZonePropertyChanged(LightingZoneViewModel value) => ((FrameworkElement)Content).DataContext = LightingZone;

	private void OnPropertyResetButtonClick(object sender, RoutedEventArgs e) => ((PropertyViewModel)((FrameworkElement)sender).DataContext).Reset();

	// Works around the bug that prevents ColorPicker.CustomPalette from being styled 😐
	private void OnColorPickerButtonLoaded(object sender, RoutedEventArgs e) => ((ColorPickerButton)sender).ColorPicker.CustomPalette = (IColorPalette)this.FindResource("RgbLightingDefaultPalette");

	private void OnEffectResetButtonClick(object sender, RoutedEventArgs e) => ((LightingZoneViewModel)((FrameworkElement)sender).DataContext).Reset();
}
