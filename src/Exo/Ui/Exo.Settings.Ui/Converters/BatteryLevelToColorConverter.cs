using Microsoft.UI;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class BatteryLevelToColorConverter : IValueConverter
{
	private static readonly object Lime = Colors.Lime;
	private static readonly object Orange = Colors.Orange;
	private static readonly object Red = Colors.Red;

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is float f)
		{
			if (f <= 0.2f)
			{
				if (f <= 0.1f)
				{
					return Red;
				}
				return Orange;
			}
		}
		return Lime;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
