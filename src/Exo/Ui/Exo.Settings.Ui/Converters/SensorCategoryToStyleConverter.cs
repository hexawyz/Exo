using Exo.Metadata;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class SensorCategoryToStyleConverter : DependencyObject, IValueConverter
{
	public Style? DefaultStyle
	{
		get => (Style?)GetValue(DefaultStyleProperty);
		set => SetValue(DefaultStyleProperty, value);
	}

	public static readonly DependencyProperty DefaultStyleProperty = DependencyProperty.Register(nameof(DefaultStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? PercentageStyle
	{
		get => (Style?)GetValue(PercentageStyleProperty);
		set => SetValue(PercentageStyleProperty, value);
	}

	public static readonly DependencyProperty PercentageStyleProperty = DependencyProperty.Register(nameof(PercentageStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? FrequencyStyle
	{
		get => (Style?)GetValue(FrequencyStyleProperty);
		set => SetValue(FrequencyStyleProperty, value);
	}

	public static readonly DependencyProperty FrequencyStyleProperty = DependencyProperty.Register(nameof(FrequencyStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? FanStyle
	{
		get => (Style?)GetValue(FanStyleProperty);
		set => SetValue(FanStyleProperty, value);
	}

	public static readonly DependencyProperty FanStyleProperty = DependencyProperty.Register(nameof(FanStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? PumpStyle
	{
		get => (Style?)GetValue(PumpStyleProperty);
		set => SetValue(PumpStyleProperty, value);
	}

	public static readonly DependencyProperty PumpStyleProperty = DependencyProperty.Register(nameof(PumpStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? TemperatureStyle
	{
		get => (Style?)GetValue(TemperatureStyleProperty);
		set => SetValue(TemperatureStyleProperty, value);
	}

	public static readonly DependencyProperty TemperatureStyleProperty = DependencyProperty.Register(nameof(TemperatureStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? PowerStyle
	{
		get => (Style?)GetValue(PowerStyleProperty);
		set => SetValue(PowerStyleProperty, value);
	}

	public static readonly DependencyProperty PowerStyleProperty = DependencyProperty.Register(nameof(PowerStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? VoltageStyle
	{
		get => (Style?)GetValue(VoltageStyleProperty);
		set => SetValue(VoltageStyleProperty, value);
	}

	public static readonly DependencyProperty VoltageStyleProperty = DependencyProperty.Register(nameof(VoltageStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public Style? CurrentStyle
	{
		get => (Style?)GetValue(CurrentStyleProperty);
		set => SetValue(CurrentStyleProperty, value);
	}

	public static readonly DependencyProperty CurrentStyleProperty = DependencyProperty.Register(nameof(CurrentStyle), typeof(Style), typeof(SensorCategoryToStyleConverter), new PropertyMetadata(null));

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not SensorCategory category) return DefaultStyle;

		return category switch
		{
			SensorCategory.Other => DefaultStyle,
			SensorCategory.Load => PercentageStyle,
			SensorCategory.Frequency => FrequencyStyle,
			SensorCategory.Fan => FanStyle,
			SensorCategory.Pump => PumpStyle,
			SensorCategory.Temperature => TemperatureStyle,
			SensorCategory.Power => PowerStyle,
			SensorCategory.Voltage => VoltageStyle,
			SensorCategory.Current => CurrentStyle,
			_ => DefaultStyle,
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
