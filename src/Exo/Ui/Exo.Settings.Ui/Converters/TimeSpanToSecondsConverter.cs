using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class TimeSpanToSecondsConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is TimeSpan t)
		{
			return t.TotalSeconds;
		}
		return null;
	}

	public object? ConvertBack(object value, Type targetType, object parameter, string language)
	{
		if (value is double d)
		{
			return TimeSpan.FromSeconds(d);
		}
		return null;
	}
}
