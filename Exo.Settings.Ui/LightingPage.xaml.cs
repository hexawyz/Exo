using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
		App.Current.Services.GetRequiredService<IEditionService>().ShowToolbar = true;
	}

	protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
	{
		App.Current.Services.GetRequiredService<IEditionService>().ShowToolbar = false;
		base.OnNavigatingFrom(e);
	}
}
