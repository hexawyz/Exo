using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Controls;
using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
	// Also works around the limitation that forces the color picker to be constrained to bounds. 😩
	private void OnColorPickerButtonLoaded(object sender, RoutedEventArgs e)
	{
		var button = (ColorPickerButton)sender;

		button.ColorPicker.CustomPalette = (IColorPalette)this.FindResource("RgbLightingDefaultPalette");
		button.Flyout.ShouldConstrainToRootBounds = false;
		button.Flyout.SystemBackdrop = new DesktopAcrylicBackdrop();
	}

	private void OnEffectResetButtonClick(object sender, RoutedEventArgs e) => ((LightingZoneViewModel)((FrameworkElement)sender).DataContext).Reset();

	private void OnColorSwatchButtonClick(object sender, RoutedEventArgs e)
	{
		var dataContext = ((FrameworkElement)sender).DataContext;
		((ArrayElementViewModel)dataContext).Value = App.Current.Services.GetRequiredService<IEditionService>().Color;
    }
}
