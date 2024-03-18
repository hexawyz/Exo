using CommunityToolkit.WinUI.UI.Controls;
using CommunityToolkit.WinUI.UI;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class LightingPage : Page
{
	public LightingPage()
	{
		InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

	private async void OnDeviceApplyButtonClick(object sender, RoutedEventArgs e) => await ((LightingDeviceViewModel)((FrameworkElement)sender).DataContext).ApplyChangesAsync(default);

	private void OnDeviceResetButtonClick(object sender, RoutedEventArgs e) => ((LightingDeviceViewModel)((FrameworkElement)sender).DataContext).Reset();

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		ViewModel.Icon = "\uE781";
		ViewModel.Title = "Lighting";
	}

	// Works around the bug that prevents ColorPicker.CustomPalette from being styled 😐
	// Also works around the limitation that forces the color picker to be constrained to bounds. 😩
	private void OnColorPickerButtonLoaded(object sender, RoutedEventArgs e)
	{
		var button = (ColorPickerButton)sender;

		button.ColorPicker.CustomPalette = (IColorPalette)this.FindResource("RgbLightingDefaultPalette");
		button.Flyout.ShouldConstrainToRootBounds = false;
		button.Flyout.SystemBackdrop = new DesktopAcrylicBackdrop();
	}
}
