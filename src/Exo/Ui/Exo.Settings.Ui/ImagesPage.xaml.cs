using Exo.Memory;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ImagesPage : Page
    {
        public ImagesPage()
        {
            InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;
}
