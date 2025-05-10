using Exo.Features;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class BatteryStateToGlyphConverter : IValueConverter
{
	private static readonly string[] DischargingGlyphs = new[]
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

	private static readonly string[] ChargingGlyphs = new[]
	{
		"\uEBAB",
		"\uEBAC",
		"\uEBAD",
		"\uEBAE",
		"\uEBAF",
		"\uEBB0",
		"\uEBB1",
		"\uEBB2",
		"\uEBB3",
		"\uEBB4",
		"\uEBB5",
	};

	private static readonly string UnknownGlyph = "\uEC02";

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not ViewModels.BatteryStateViewModel state) return UnknownGlyph;

		int batteryLevel = 0;

		bool isPowered = (state.ExternalPowerStatus & ExternalPowerStatus.IsConnected) != 0;
		bool isChargingOrComplete = state.BatteryStatus is BatteryStatus.Charging or BatteryStatus.ChargingNearlyComplete or BatteryStatus.ChargingComplete;

		if (state.BatteryStatus == BatteryStatus.ChargingComplete)
			batteryLevel = 10;
		else if (state.Level is null)
			// If the device can't report battery level while charging, we'll still show up an icon, but we have to make conservative estimates.
			if (state.BatteryStatus is BatteryStatus.ChargingNearlyComplete)
				batteryLevel = 9;
			else if (state.BatteryStatus is BatteryStatus.Charging)
				// This is the smallest non-zero status that we can display.
				// It may not be representative of the actual battery status, but it will indicate that it is charging.
				batteryLevel = 1;
			else
				return UnknownGlyph;
		else
		{
			float level = state.Level.GetValueOrDefault();

			if (state.BatteryStatus == BatteryStatus.ChargingNearlyComplete)
				// Adjust the batteryLevel to make sure we display something akin to a nearly charged battery.
				batteryLevel = level < 0.95f ? 9 : 10;
			else if (level is >= 0)
				if (level >= 1)
					batteryLevel = 10;
				else
					// Bias the glyphs by 5pt of value, so that e.g. 99% is displayed as 100%, and only < 5% is displayed at 0%.
					batteryLevel = (int)(level * 10 + 0.5f);
		}

		return (isPowered && isChargingOrComplete ? ChargingGlyphs : DischargingGlyphs)[batteryLevel];
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
