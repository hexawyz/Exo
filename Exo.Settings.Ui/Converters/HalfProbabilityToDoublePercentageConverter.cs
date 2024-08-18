using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class HalfProbabilityToDoublePercentageConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is Half h)
		{
			return (double)h * 100;
		}
		return null;
	}

	public object? ConvertBack(object value, Type targetType, object parameter, string language)
	{
		if (value is double d)
		{
			return (Half)(d / 100);
		}
		return null;
	}
}
