using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class NotANumberToVisibilityConverter : IValueConverter
{
	private static readonly object Visible = Visibility.Visible;
	private static readonly object Collapsed = Visibility.Collapsed;

	public object? Convert(object value, Type targetType, object parameter, string language)
		=> value switch
		{
			Half h => Half.IsNaN(h) ? Collapsed : Visible,
			float f => float.IsNaN(f) ? Collapsed : Visible,
			double d => double.IsNaN(d) ? Collapsed : Visible,
			_ => Collapsed,
		};

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
