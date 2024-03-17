using Exo.Contracts;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed class EffectPropertyTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate NumericRangeTemplate { get; set; }
	public DataTemplate NumericTemplate { get; set; }
	public DataTemplate BrightnessTemplate { get; set; }
	public DataTemplate TextTemplate { get; set; }
	public DataTemplate GrayscaleTemplate { get; set; }
	public DataTemplate ColorTemplate { get; set; }
	public DataTemplate DateTimeTemplate { get; set; }
	public DataTemplate TimeSpanTemplate { get; set; }
	public DataTemplate EnumTemplate { get; set; }
	public DataTemplate EnumRangeTemplate { get; set; }
	public DataTemplate ColorArrayTemplate { get; set; }
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
			case DataType.UInt8:
			case DataType.Int8:
			case DataType.UInt16:
			case DataType.Int16:
			case DataType.UInt32:
			case DataType.Int32:
			case DataType.UInt64:
			case DataType.Int64:
				if (sp.EnumerationValues.Count > 0)
				{
					if (sp.MinimumValue is not null && sp.MaximumValue is not null)
					{
						return EnumRangeTemplate;
					}
					return EnumTemplate;
				}
				else if (sp.Name == "BrightnessLevel")
				{
					return BrightnessTemplate;
				}
				goto case DataType.Float16;
			case DataType.Float16:
			case DataType.Float32:
			case DataType.Float64:
				return sp.MinimumValue is not null && sp.MaximumValue is not null ? NumericRangeTemplate : NumericTemplate;
			case DataType.String:
				return TextTemplate;
			case DataType.TimeSpan:
				return TimeSpanTemplate;
			case DataType.DateTime:
				return DateTimeTemplate;
			case DataType.ColorGrayscale8:
			case DataType.ColorGrayscale16:
				return GrayscaleTemplate;
			case DataType.ColorRgb24:
			case DataType.ColorArgb32:
				return ColorTemplate;
			}
		}
		else if (item is FixedLengthArrayPropertyViewModel ap)
		{
			switch (ap.DataType)
			{
			case DataType.ArrayOfColorRgb24:
				return ColorArrayTemplate;
			}
		}
		return FallbackTemplate;
	}
}
