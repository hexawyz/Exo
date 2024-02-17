using Exo.Settings.Ui.ViewModels;
using Exo.Ui.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed class MonitorSettingTemplateSelector : DataTemplateSelector
{
	public DataTemplate BrightnessTemplate { get; set; }
	public DataTemplate ContrastTemplate { get; set; }
	public DataTemplate DefaultContinuousTemplate { get; set; }

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
		=> SelectTemplateCore(item);

	protected override DataTemplate SelectTemplateCore(object item)
	{
		if (item is MonitorDeviceSettingViewModel s)
		{
			switch (s.Setting)
			{
			case MonitorSetting.Brightness: return BrightnessTemplate;
			case MonitorSetting.Contrast: return ContrastTemplate;
			}
		}
		return DefaultContinuousTemplate;
	}
}
