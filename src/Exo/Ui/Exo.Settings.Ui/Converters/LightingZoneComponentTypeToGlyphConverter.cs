using Exo.Metadata;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

public class LightingZoneComponentTypeToGlyphConverter : IValueConverter
{
	// ic_fluent_layout_cell_four_20_filled (Also tried: ic_fluent_developer_board_20_regular, and ic_fluent_connected_20_regular but it doesn't look good in that case TBH; Other alternative: ic_fluent_iot_20_regular)
	private const string DefaultGlyph = "\uEA2E";

	private static readonly Dictionary<LightingZoneComponentType, string> DeviceCategoryToGlyphMapping = new()
	{
		{ LightingZoneComponentType.Unknown, DefaultGlyph },
		// ic_fluent_data_sunburst_20_filled (Or maybe ic_fluent_new_20_filled; There doesn't seem to be a LED icon in this font 🙁)
		{ LightingZoneComponentType.Indicator, "\uE5DC" },
		// ic_fluent_image_circle_20_regular (Or maybe ic_fluent_data_waterfall_20_filled)
		{ LightingZoneComponentType.Logo, "\uE9B6" },
		// ic_fluent_line_horizontal_1_dot_20_filled
		{ LightingZoneComponentType.Strip, "\uEA91" },
		// ic_fluent_scan_20_regular (Close enough I guess; other option ic_fluent_data_sunburst_20_filled)
		{ LightingZoneComponentType.Fan, "\uEEBB" },
		// ic_fluent_washer_20_regular (For lack of a better option)
		{ LightingZoneComponentType.Pump, "\uF41B" },
		// ic_fluent_ram_20_regular
		{ LightingZoneComponentType.MemoryStick, "\uEE2B" },
		// ic_fluent_button_20_regular (Can probably find better)
		{ LightingZoneComponentType.Button, "\uE2F4" },
		// ic_fluent_hexagon_three_20_filled
		{ LightingZoneComponentType.Tiles, "\uE97C" },
	};

	public static string GetGlyph(LightingZoneComponentType category)
		=> DeviceCategoryToGlyphMapping.TryGetValue(category, out string? glyph) ? glyph : DefaultGlyph;

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not LightingZoneComponentType category)
			category = LightingZoneComponentType.Unknown;

		return GetGlyph(category);
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
