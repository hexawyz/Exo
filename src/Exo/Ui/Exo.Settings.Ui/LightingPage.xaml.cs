using System.ComponentModel;
using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class LightingPage : Page
{
	public SettingsViewModel SettingsViewModel { get; }
	public LightingViewModel Lighting { get; }
	public IEditionService EditionService { get; }

	public LightingPage()
	{
		SettingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
		Lighting = SettingsViewModel.Lighting;
		EditionService = App.Current.Services.GetRequiredService<IEditionService>();
		InitializeComponent();
		// Bloated manual binding to work around usual fundamental bugs from WinUI.
		EditColorPicker.Loaded += OnColorPickerLoaded;
		EditColorPicker.Unloaded += OnColorPickerUnloaded;
	}

	private void OnColorPickerLoaded(object sender, RoutedEventArgs e)
	{
		if (EditColorPicker.Color != EditionService.Color)
		{
			EditColorPicker.Color = EditionService.Color;
		}
		EditColorPicker.ColorChanged += OnColorChanged;
		EditionService.PropertyChanged += OnEditionServicePropertyChanged;
	}

	private void OnColorPickerUnloaded(object sender, RoutedEventArgs e)
	{
		EditionService.PropertyChanged -= OnEditionServicePropertyChanged;
		EditColorPicker.ColorChanged -= OnColorChanged;
	}

	private void OnColorChanged(ColorPicker sender, ColorChangedEventArgs args)
	{
		EditionService.Color = EditColorPicker.Color;
	}

	private void OnEditionServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		EditColorPicker.Color = EditionService.Color;
	}
}
