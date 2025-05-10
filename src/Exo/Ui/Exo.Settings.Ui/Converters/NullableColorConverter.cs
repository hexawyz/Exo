using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class NullableColorConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value as Color? ?? Colors.Transparent;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value as Color? ?? Colors.Transparent;
}
