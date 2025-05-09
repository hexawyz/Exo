using System.ComponentModel;
using Exo.Settings.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

public sealed partial class LightingPage : Page
{
	private readonly IEditionService _editionService;

	public LightingPage()
	{
		// Bloated manual binding to work around usual fundamental bugs from WinUI.
		_editionService = App.Current.Services.GetRequiredService<IEditionService>();
		InitializeComponent();
		EditColorPicker.Loaded += OnColorPickerLoaded;
		EditColorPicker.Unloaded += OnColorPickerUnloaded;
	}

	private void OnColorPickerLoaded(object sender, RoutedEventArgs e)
	{
		EditColorPicker.Color = _editionService.Color;
		EditColorPicker.ColorChanged += OnColorChanged;
		_editionService.PropertyChanged += OnEditionServicePropertyChanged;
	}

	private void OnColorPickerUnloaded(object sender, RoutedEventArgs e)
	{
		_editionService.PropertyChanged -= OnEditionServicePropertyChanged;
		EditColorPicker.ColorChanged -= OnColorChanged;
	}

	private void OnColorChanged(ColorPicker sender, ColorChangedEventArgs args)
	{
		_editionService.Color = EditColorPicker.Color;
	}

	private void OnEditionServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		EditColorPicker.Color = _editionService.Color;
	}
}
