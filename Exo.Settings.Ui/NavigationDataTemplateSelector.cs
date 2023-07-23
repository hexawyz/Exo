using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Exo.Settings.Ui;

internal sealed class NavigationDataTemplateSelector : DataTemplateSelector
{
	public DataTemplate DeviceDataTemplate { get; set; }
	public DataTemplate DefaultDataTemplate { get; set; }

	protected override DataTemplate SelectTemplateCore(object item)
	{
		if (item is DeviceViewModel) return DeviceDataTemplate;
		else return DefaultDataTemplate;
	}
}
