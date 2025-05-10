using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class SensorsPage : Page
{
	public SensorsViewModel Sensors { get; }

	public SensorsPage()
	{
		Sensors = App.Current.Services.GetRequiredService<SettingsViewModel>().Sensors;
		InitializeComponent();
	}
}
