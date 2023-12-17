using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Exo.Overlay;

internal sealed class NonZeroToVisibilityHiddenConverter : IValueConverter
{
	private static readonly object Hidden = Visibility.Hidden;
	private static readonly object Visible = Visibility.Visible;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> System.Convert.ToInt32(value) == 0 ? Visible : Hidden;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
