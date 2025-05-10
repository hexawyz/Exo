using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class SingleToDoubleConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is float f ? (double)f : DependencyProperty.UnsetValue;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is double d ? (float)d : null;
}
