using Exo.ColorFormats;
using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class RgbColorConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value is RgbColor color ? Color.FromArgb(255, color.R, color.G, color.B) : null;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value is Color color ? new RgbColor(color.R, color.G, color.B) : null;
}
