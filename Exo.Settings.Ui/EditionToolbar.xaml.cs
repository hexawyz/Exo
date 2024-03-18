using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Controls;
using Exo.Settings.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui;

public sealed partial class EditionToolbar : UserControl
{
	public EditionToolbar()
	{
		DataContext = App.Current.Services.GetRequiredService<IEditionService>();
		InitializeComponent();
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
