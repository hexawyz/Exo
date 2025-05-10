using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class IntegerPercentValueConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
		=> value is not null ? $"{System.Convert.ToInt32(value)} %" : null;

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
