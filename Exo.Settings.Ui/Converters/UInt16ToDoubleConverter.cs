using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class UInt16ToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is not null ? System.Convert.ToDouble(value) : null;

	public object ConvertBack(object value, Type targetType, object parameter, string language) => System.Convert.ToUInt16(value);
}
