using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class HomePage : Page
{
	private readonly SettingsViewModel _settingsViewModel;

	public HomePage()
	{
		_settingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
		InitializeComponent();
	}

	public SettingsViewModel SettingsViewModel => _settingsViewModel;
}
