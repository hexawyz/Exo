using Exo.Lighting;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui.DataTemplateSelectors;

internal sealed partial class EffectPropertyTemplateSelector : DataTemplateSelector
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DataTemplate ByteNumericRangeTemplate { get; set; }
	public DataTemplate SByteNumericRangeTemplate { get; set; }
	public DataTemplate UInt16NumericRangeTemplate { get; set; }
	public DataTemplate Int16NumericRangeTemplate { get; set; }
	public DataTemplate UInt32NumericRangeTemplate { get; set; }
	public DataTemplate Int32NumericRangeTemplate { get; set; }
	public DataTemplate UInt64NumericRangeTemplate { get; set; }
	public DataTemplate Int64NumericRangeTemplate { get; set; }
	public DataTemplate HalfNumericRangeTemplate { get; set; }
	public DataTemplate SingleNumericRangeTemplate { get; set; }
	public DataTemplate DoubleNumericRangeTemplate { get; set; }
	public DataTemplate ByteNumericTemplate { get; set; }
	public DataTemplate SByteNumericTemplate { get; set; }
	public DataTemplate UInt16NumericTemplate { get; set; }
	public DataTemplate Int16NumericTemplate { get; set; }
	public DataTemplate UInt32NumericTemplate { get; set; }
	public DataTemplate Int32NumericTemplate { get; set; }
	public DataTemplate UInt64NumericTemplate { get; set; }
	public DataTemplate Int64NumericTemplate { get; set; }
	public DataTemplate HalfNumericTemplate { get; set; }
	public DataTemplate SingleNumericTemplate { get; set; }
	public DataTemplate DoubleNumericTemplate { get; set; }
	public DataTemplate BooleanTemplate { get; set; }
	public DataTemplate BrightnessTemplate { get; set; }
	public DataTemplate TextTemplate { get; set; }
	public DataTemplate GrayscaleTemplate { get; set; }
	public DataTemplate Direction1DTemplate { get; set; }
	public DataTemplate ColorTemplate { get; set; }
	public DataTemplate DateTimeTemplate { get; set; }
	public DataTemplate TimeSpanTemplate { get; set; }
	public DataTemplate ByteEnumTemplate { get; set; }
	public DataTemplate SByteEnumTemplate { get; set; }
	public DataTemplate UInt16EnumTemplate { get; set; }
	public DataTemplate Int16EnumTemplate { get; set; }
	public DataTemplate UInt32EnumTemplate { get; set; }
	public DataTemplate Int32EnumTemplate { get; set; }
	public DataTemplate UInt64EnumTemplate { get; set; }
	public DataTemplate Int64EnumTemplate { get; set; }
	public DataTemplate ByteEnumRangeTemplate { get; set; }
	public DataTemplate SByteEnumRangeTemplate { get; set; }
	public DataTemplate UInt16EnumRangeTemplate { get; set; }
	public DataTemplate Int16EnumRangeTemplate { get; set; }
	public DataTemplate UInt32EnumRangeTemplate { get; set; }
	public DataTemplate Int32EnumRangeTemplate { get; set; }
	public DataTemplate UInt64EnumRangeTemplate { get; set; }
	public DataTemplate Int64EnumRangeTemplate { get; set; }
	public DataTemplate VariableColorArrayTemplate { get; set; }
	public DataTemplate FixedColorArrayTemplate { get; set; }
	public DataTemplate FallbackTemplate { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
		=> SelectTemplateCore(item);

	protected override DataTemplate SelectTemplateCore(object item)
	{
		if (item is not PropertyViewModel p) return FallbackTemplate;

		if (p.IsArray)
		{
			if (item is RgbColorArrayPropertyViewModel ap)
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
		}
		else
		{
			switch (p.DataType)
			{
			case LightingDataType.UInt8:
				if (p is BrightnessPropertyViewModel) return BrightnessTemplate;
				return p is ByteEnumPropertyViewModel ?
					p.IsRange ? ByteEnumRangeTemplate : ByteEnumTemplate :
					p.IsRange ? ByteNumericRangeTemplate : ByteNumericTemplate;
			case LightingDataType.SInt8:
				return p is SByteEnumPropertyViewModel ?
					p.IsRange ? SByteEnumRangeTemplate : SByteEnumTemplate :
					p.IsRange ? SByteNumericRangeTemplate : SByteNumericTemplate;
			case LightingDataType.UInt16:
				return p is UInt16EnumPropertyViewModel ?
					p.IsRange ? UInt16EnumRangeTemplate : UInt16EnumTemplate :
					p.IsRange ? UInt16NumericRangeTemplate : UInt16NumericTemplate;
			case LightingDataType.SInt16:
				return p is Int16EnumPropertyViewModel ?
					p.IsRange ? Int16EnumRangeTemplate : Int16EnumTemplate :
					p.IsRange ? Int16NumericRangeTemplate : Int16NumericTemplate;
			case LightingDataType.UInt32:
				return p is UInt32EnumPropertyViewModel ?
					p.IsRange ? UInt32EnumRangeTemplate : UInt32EnumTemplate :
					p.IsRange ? UInt32NumericRangeTemplate : UInt32NumericTemplate;
			case LightingDataType.SInt32:
				return p is Int32EnumPropertyViewModel ?
					p.IsRange ? Int32EnumRangeTemplate : Int32EnumTemplate :
					p.IsRange ? Int32NumericRangeTemplate : Int32NumericTemplate;
			case LightingDataType.UInt64:
				return p is UInt64EnumPropertyViewModel ?
					p.IsRange ? UInt64EnumRangeTemplate : UInt64EnumTemplate :
					p.IsRange ? UInt64NumericRangeTemplate : UInt64NumericTemplate;
			case LightingDataType.SInt64:
				return p is Int64EnumPropertyViewModel ?
					p.IsRange ? Int64EnumRangeTemplate : Int64EnumTemplate :
					p.IsRange ? Int64NumericRangeTemplate : Int64NumericTemplate;
			case LightingDataType.Float16:
				return p.IsRange ? HalfNumericRangeTemplate : HalfNumericTemplate;
			case LightingDataType.Float32:
				return p.IsRange ? SingleNumericRangeTemplate : SingleNumericTemplate;
			case LightingDataType.Float64:
				return p.IsRange ? DoubleNumericRangeTemplate : DoubleNumericTemplate;
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
		return FallbackTemplate;
	}
}
