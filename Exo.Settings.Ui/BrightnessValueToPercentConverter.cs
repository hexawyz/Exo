using System;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui;

internal sealed class BrightnessValueToPercentConverter : DependencyObject, IValueConverter
{
	public LightingDeviceBrightnessViewModel? BrightnessRange
	{
		get => (LightingDeviceBrightnessViewModel)GetValue(BrightnessRangeProperty);
		set => SetValue(BrightnessRangeProperty, value);
	}

	// Using a DependencyProperty as the backing store for Values.  This enables animation, styling, binding, etc...
	public static readonly DependencyProperty BrightnessRangeProperty =
		DependencyProperty.Register
		(
			"BrightnessRange",
			typeof(LightingDeviceBrightnessViewModel),
			typeof(BrightnessValueToPercentConverter),
			new PropertyMetadata(null)
		);

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not null && BrightnessRange is { } range)
		{
			byte b = System.Convert.ToByte(value);

			double percent = b / (double)range.MaximumLevel;

			if (percent < 0) percent = 0;
			else if (percent > 1) percent = 1;

			return percent.ToString("P0");
		}

		return value?.ToString();
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
