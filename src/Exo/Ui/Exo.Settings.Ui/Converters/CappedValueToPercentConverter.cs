using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class CappedValueToPercentConverter : DependencyObject, IValueConverter
{
	public int MaximumValue
	{
		get => System.Convert.ToInt32(GetValue(MaximumValueProperty));
		set => SetValue(MaximumValueProperty, System.Convert.ToInt32(value));
	}

	public static readonly DependencyProperty MaximumValueProperty =
		DependencyProperty.Register("MaximumValue", typeof(int), typeof(CappedValueToPercentConverter), new PropertyMetadata(100));

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not null && MaximumValue is var max and > 0)
		{
			int v = System.Convert.ToInt32(value);

			double percent = v / (double)max;

			string format = max > 100 ? "P1" : "P0";

			if (percent < 0) percent = 0;
			else if (percent > 1) percent = 1;

			return percent.ToString(format);
		}

		return value?.ToString();
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
