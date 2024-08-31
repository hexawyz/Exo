using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Exo.Overlay;

internal class GlyphFontToFontFamilyConverter : IValueConverter
{
	public FontFamily? SegoeFluentIcons { get; set; }
	public FontFamily? FluentSystemIcons { get; set; }

	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value switch { GlyphFont.SegoeFluentIcons => SegoeFluentIcons, GlyphFont.FluentSystemIcons => FluentSystemIcons, _ => null };

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
