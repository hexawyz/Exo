using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Exo.Settings.Ui;

public sealed partial class SensorsPage : Page
{
	public SensorsPage()
	{
		InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		ViewModel.Icon = "\uE9D9";
		ViewModel.Title = "Sensors";
	}
}
