using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StreamDeckPlayground;

public sealed class NullabilityToVisibilityConverter : IValueConverter
{
	private static readonly object Visible = Visibility.Visible;
	private static readonly object Collapsed = Visibility.Collapsed;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not null ? Visible : Collapsed;
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
