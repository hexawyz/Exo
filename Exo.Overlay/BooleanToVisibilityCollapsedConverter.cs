using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Exo.Overlay;

internal class BooleanToVisibilityCollapsedConverter : IValueConverter
{
	private static readonly object Collapsed = Visibility.Collapsed;
	private static readonly object Visible = Visibility.Visible;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is true ? Visible : Collapsed;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
