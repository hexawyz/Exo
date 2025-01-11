using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class GridLengthConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is not null ? new GridLength(System.Convert.ToDouble(value)) : null;

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
