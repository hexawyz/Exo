using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class SecondsToStringConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is double d)
		{
			return TimeSpan.FromSeconds(d).ToString("g");
		}
		return null;
	}

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
