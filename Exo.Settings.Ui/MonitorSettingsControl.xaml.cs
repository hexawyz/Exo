using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class MonitorSettingsControl : UserControl
{
	public MonitorSettingsControl()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
	{
		if (args.NewValue is MonitorDeviceFeaturesViewModel vm)
		{
			vm.RefreshCommand.Execute(vm);
		}
	}
}
