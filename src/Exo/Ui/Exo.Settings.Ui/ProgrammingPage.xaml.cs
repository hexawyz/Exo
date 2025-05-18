using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class ProgrammingPage : Page
{
	public SettingsViewModel SettingsViewModel { get; }
	public ProgrammingViewModel Programming { get; }

	public ProgrammingPage()
	{
		SettingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
		Programming = SettingsViewModel.Programming;
		InitializeComponent();
	}
}
