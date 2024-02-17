using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui;

internal sealed class StringFormatConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (parameter is string format)
		{
			if (value is IFormattable formattable)
			{
				return formattable.ToString(format, null);
			}
		}
		return value?.ToString();
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
