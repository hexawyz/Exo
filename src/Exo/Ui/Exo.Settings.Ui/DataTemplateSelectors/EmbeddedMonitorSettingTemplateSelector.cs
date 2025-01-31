using Exo.Settings.Ui.ViewModels;
using Exo.Contracts.Ui.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui.DataTemplateSelectors;

internal sealed class EmbeddedMonitorSettingTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate SingleMonitorTemplate { get; set; }
	public DataTemplate MonitorMatrixTemplate { get; set; }
	public DataTemplate MultiMonitorTemplate { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
		=> SelectTemplateCore(item);

	protected override DataTemplate SelectTemplateCore(object item)
	{
		// NB: This can't detect changes. If we ever need to update the display mode dynamically, it will have to be driven externally. (For example triggering a PropertyChanged event somewhere)
		if (item is EmbeddedMonitorFeaturesViewModel s)
		{
			if (s.EmbeddedMonitors.Count == 1) return SingleMonitorTemplate;
			// TODO: This is hardcoded for StreamDeck. Have this less hardcoded. Should probably be driven by the ViewModel + metadata.
			else if (s.EmbeddedMonitors.Count == 32) return MonitorMatrixTemplate;
		}
		return MultiMonitorTemplate;
	}
}
