using Exo.Metadata;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

public class LightingZoneComponentTypeToGlyphConverter : IValueConverter
{
	private static readonly Dictionary<LightingZoneComponentType, string> DeviceCategoryToGlyphMapping = new()
	{
		// NB: These glyphs are not that good. Would have to use the more complete font.
		{ LightingZoneComponentType.Unknown, "\uE950" },
		{ LightingZoneComponentType.Indicator, "\uE781" },
		{ LightingZoneComponentType.Strip, "\uE781" },
		{ LightingZoneComponentType.Fan, "\uE9CA" },
		{ LightingZoneComponentType.Pump, "\uE9CA" },
	};

	public static string GetGlyph(LightingZoneComponentType category)
		=> DeviceCategoryToGlyphMapping.TryGetValue(category, out string? glyph) ? glyph : "\uE950";

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not LightingZoneComponentType category)
			category = LightingZoneComponentType.Unknown;

		return GetGlyph(category);
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
