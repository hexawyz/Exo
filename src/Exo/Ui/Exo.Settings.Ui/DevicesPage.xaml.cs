using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class DevicesPage : Page
{
	public DevicesViewModel Devices { get; }

	public DevicesPage()
	{
		Devices = App.Current.Services.GetRequiredService<SettingsViewModel>().Devices;
		InitializeComponent();
	}
}
