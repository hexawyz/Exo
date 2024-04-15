using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui;

internal sealed class HistoryDataToPathGeometryConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
		=> value is LiveSensorDetailsViewModel.HistoryData history ? Convert(history, parameter is not null && System.Convert.ToBoolean(parameter)) : null;

	private PathGeometry Convert(LiveSensorDetailsViewModel.HistoryData history, bool closeShape)
	{
		// NB: This is very rough and WIP.
		// It should probably be ported to a dedicated chart drawing component afterwards.

		double minValue = double.PositiveInfinity;
		double maxValue = double.NegativeInfinity;

		for (int i = 0; i < history.Length; i++)
		{
			double value = history[i];
			minValue = Math.Min(value, minValue);
			maxValue = Math.Max(value, maxValue);
		}

		// Anchor the scale to zero if necessary.
		if (maxValue < 0) maxValue = 0;
		if (minValue > 0) minValue = 0;

		// Force the chart to not be fully empty if the min and max are both zero. (result of previous adjustments)
		if (minValue == maxValue) maxValue = 1;

		var (scaleMin, scaleMax, _) = NiceScale.Compute(minValue, maxValue);

		double scaleAmplitudeX = history.Length - 1;
		double scaleAmplitudeY = maxValue - minValue;
		double outputAmplitudeX = 99;
		double outputAmplitudeY = 99;

		var figure = new PathFigure();

		double firstValue;
		double firstValueY;
		int j;
		if (closeShape)
		{
			firstValue = 0;
			j = 0;
		}
		else
		{
			firstValue = history[0];
			j = 1;
		}

		figure.StartPoint = new(0.5, 0.5f + (firstValueY = outputAmplitudeY - (firstValue - minValue) * outputAmplitudeY / scaleAmplitudeY));
		for (; j < history.Length; j++)
		{
			double value = history[j];
			double x = j * outputAmplitudeX / scaleAmplitudeX;
			double y = outputAmplitudeY - (value - minValue) * outputAmplitudeY / scaleAmplitudeY;
			figure.Segments.Add(new LineSegment() { Point = new(x + 0.5, y + 0.5) });
		}

		if (closeShape)
		{
			figure.Segments.Add(new LineSegment() { Point = new(outputAmplitudeX + 0.5, firstValueY + 0.5) });
		}

		return new PathGeometry() { Figures = { figure } };
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

// See: https://stackoverflow.com/a/16363437
internal static class NiceScale
{
	public static (double Min, double Max, double TickSpacing) Compute(double min, double max, double tickCount = 10)
	{
		double range = MakeNice(max - min, false);
		double tickSpacing = MakeNice(range / (tickCount - 1), true);
		double niceMin = min < 0 ?
			Math.Ceiling(min / tickSpacing) * tickSpacing :
			Math.Floor(min / tickSpacing) * tickSpacing;
		double niceMax = min < 0 ?
			Math.Floor(max / tickSpacing) * tickSpacing :
			Math.Ceiling(max / tickSpacing) * tickSpacing;

		return (niceMin, niceMax, tickSpacing);
	}

	public static double MakeNice(double number, bool round)
	{
		double exponent;
		double fraction;
		double niceFraction;

		exponent = Math.Floor(Math.Log10(number));
		fraction = number / Math.Pow(10, exponent);

		if (round)
		{
			if (fraction < 1.5)
				niceFraction = 1;
			else if (fraction < 3)
				niceFraction = 2;
			else if (fraction < 7)
				niceFraction = 5;
			else
				niceFraction = 10;
		}
		else
		{
			if (fraction <= 1)
				niceFraction = 1;
			else if (fraction <= 2)
				niceFraction = 2;
			else if (fraction <= 5)
				niceFraction = 5;
			else
				niceFraction = 10;
		}

		return niceFraction * Math.Pow(10, exponent);
	}
}
