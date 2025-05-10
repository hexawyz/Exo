using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Exo.Service;

namespace Exo.Settings.Ui.DataTemplateSelectors;

internal sealed partial class MonitorSettingTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate BrightnessTemplate { get; set; }
	public DataTemplate ContrastTemplate { get; set; }
	public DataTemplate AudioVolumeTemplate { get; set; }
	public DataTemplate DefaultContinuousTemplate { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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
			case MonitorSetting.AudioVolume: return AudioVolumeTemplate;
			}
		}
		return DefaultContinuousTemplate;
	}
}
