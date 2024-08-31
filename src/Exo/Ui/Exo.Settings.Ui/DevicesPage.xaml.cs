using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class DevicesPage : Page
{
	public DevicesPage()
	{
		InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;
}
