using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Exo.Settings.Ui;

public sealed partial class LightingPage : Page
{
	public LightingPage()
	{
		InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

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
