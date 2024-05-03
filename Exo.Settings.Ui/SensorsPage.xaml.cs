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
}
