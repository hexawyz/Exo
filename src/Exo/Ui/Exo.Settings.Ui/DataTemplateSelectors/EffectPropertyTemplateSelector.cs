using Exo.Lighting;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui.DataTemplateSelectors;

internal sealed class EffectPropertyTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate NumericRangeTemplate { get; set; }
	public DataTemplate NumericTemplate { get; set; }
	public DataTemplate BooleanTemplate { get; set; }
	public DataTemplate BrightnessTemplate { get; set; }
	public DataTemplate TextTemplate { get; set; }
	public DataTemplate GrayscaleTemplate { get; set; }
	public DataTemplate Direction1DTemplate { get; set; }
	public DataTemplate ColorTemplate { get; set; }
	public DataTemplate DateTimeTemplate { get; set; }
	public DataTemplate TimeSpanTemplate { get; set; }
	public DataTemplate EnumTemplate { get; set; }
	public DataTemplate EnumRangeTemplate { get; set; }
	public DataTemplate VariableColorArrayTemplate { get; set; }
	public DataTemplate FixedColorArrayTemplate { get; set; }
	public DataTemplate FallbackTemplate { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
		=> SelectTemplateCore(item);

	protected override DataTemplate SelectTemplateCore(object item)
	{
		if (item is ScalarPropertyViewModel sp)
		{
			switch (sp.DataType)
			{
			case LightingDataType.UInt8:
			case LightingDataType.SInt8:
			case LightingDataType.UInt16:
			case LightingDataType.SInt16:
			case LightingDataType.UInt32:
			case LightingDataType.SInt32:
			case LightingDataType.UInt64:
			case LightingDataType.SInt64:
				if (sp.EnumerationValues.Count > 0)
				{
					if (sp.MinimumValue is not null && sp.MaximumValue is not null)
						return EnumRangeTemplate;
					return EnumTemplate;
				}
				else if (sp.Name == "BrightnessLevel")
					return BrightnessTemplate;
				goto case LightingDataType.Float16;
			case LightingDataType.Float16:
			case LightingDataType.Float32:
			case LightingDataType.Float64:
				return sp.MinimumValue is not null && sp.MaximumValue is not null ? NumericRangeTemplate : NumericTemplate;
			case LightingDataType.Boolean:
				return BooleanTemplate;
			case LightingDataType.String:
				return TextTemplate;
			case LightingDataType.TimeSpan:
				return TimeSpanTemplate;
			case LightingDataType.DateTime:
				return DateTimeTemplate;
			case LightingDataType.EffectDirection1D:
				return Direction1DTemplate;
			case LightingDataType.ColorGrayscale8:
			case LightingDataType.ColorGrayscale16:
				return GrayscaleTemplate;
			case LightingDataType.ColorRgb24:
			case LightingDataType.ColorArgb32:
				return ColorTemplate;
			}
		}
		else if (item is RgbColorArrayPropertyViewModel ap)
		{
			if (ap.IsVariableLength)
			{
				return VariableColorArrayTemplate;
			}
			else
			{
				return FixedColorArrayTemplate;
			}
		}
		return FallbackTemplate;
	}
}
