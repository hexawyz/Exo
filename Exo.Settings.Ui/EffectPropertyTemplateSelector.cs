using Exo.Contracts;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed class EffectPropertyTemplateSelector : DataTemplateSelector
{
	public DataTemplate NumericRangeTemplate { get; set; }
	public DataTemplate NumericTemplate { get; set; }
	public DataTemplate TextTemplate { get; set; }
	public DataTemplate GrayscaleTemplate { get; set; }
	public DataTemplate ColorTemplate { get; set; }
	public DataTemplate DateTimeTemplate { get; set; }
	public DataTemplate TimeSpanTemplate { get; set; }
	public DataTemplate EnumTemplate { get; set; }
	public DataTemplate EnumRangeTemplate { get; set; }
	public DataTemplate FallbackTemplate { get; set; }

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
		=> SelectTemplateCore(item);

	protected override DataTemplate SelectTemplateCore(object item)
	{
		if (item is PropertyViewModel p)
		{
			switch (p.DataType)
			{
			case DataType.UInt8:
			case DataType.Int8:
			case DataType.UInt16:
			case DataType.Int16:
			case DataType.UInt32:
			case DataType.Int32:
			case DataType.UInt64:
			case DataType.Int64:
				if (p.EnumerationValues.Count > 0)
				{
					if (p.MinimumValue is not null && p.MaximumValue is not null)
					{
						return EnumRangeTemplate;
					}
					return EnumTemplate;
				}
				goto case DataType.Float16;
			case DataType.Float16:
			case DataType.Float32:
			case DataType.Float64:
				return p.MinimumValue is not null && p.MaximumValue is not null ? NumericRangeTemplate : NumericTemplate;
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
		return FallbackTemplate;
	}
}
