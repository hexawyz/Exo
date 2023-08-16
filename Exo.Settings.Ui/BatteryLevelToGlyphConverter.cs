using System;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui;

internal sealed class BatteryLevelToGlyphConverter : IValueConverter
{
	private static readonly string[] Glyphs = new[]
	{
		"\uEBA0",
		"\uEBA1",
		"\uEBA2",
		"\uEBA3",
		"\uEBA4",
		"\uEBA5",
		"\uEBA6",
		"\uEBA7",
		"\uEBA8",
		"\uEBA9",
		"\uEBAA",
	};

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		int batteryLevel = 0;

		if (value is float f and >= 0)
		{
			// Bias the glyphs by 5pt of value, so that e.g. 99% is displayed as 100%, and only < 5% is displayed at 0%.
			if (f <= 1) batteryLevel = (int)(f * 10 + 0.5f);
		}

		return Glyphs[batteryLevel];
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
