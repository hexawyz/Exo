using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class CoolingPage : Page
{
	public CoolingViewModel Cooling { get; }

	public CoolingPage()
	{
		Cooling = App.Current.Services.GetRequiredService<SettingsViewModel>().Cooling;

		InitializeComponent();
	}
}
