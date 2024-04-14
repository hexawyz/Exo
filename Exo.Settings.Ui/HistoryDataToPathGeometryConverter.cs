using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui;

internal sealed class HistoryDataToPathGeometryConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
		=> value is LiveSensorDetailsViewModel.HistoryData history ? Convert(history) : null;

	private PathGeometry Convert(LiveSensorDetailsViewModel.HistoryData history)
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

		double scaleMin = Math.Round(minValue, 0, MidpointRounding.ToNegativeInfinity);
		double scaleMax = Math.Round(maxValue, 0, MidpointRounding.ToPositiveInfinity);

		// Anchor the scale to zero if necessary.
		if (maxValue < 0) maxValue = 0;
		if (minValue > 0) minValue = 0;

		// Force the chart to not be fully empty if the min and max are both zero. (result of previous adjustments)
		if (minValue == maxValue) maxValue = 1;

		double scaleAmplitudeX = history.Length - 1;
		double scaleAmplitudeY = maxValue - minValue;
		double outputAmplitudeX = 100;
		double outputAmplitudeY = 100;

		var figure = new PathFigure();

		double firstValue = history[0];

		figure.StartPoint = new(0, outputAmplitudeY - (firstValue - minValue) * outputAmplitudeY / scaleAmplitudeY);
		for (int i = 1; i < history.Length; i++)
		{
			double value = history[i];
			double x = i * outputAmplitudeX / scaleAmplitudeX;
			double y = outputAmplitudeY - (value - minValue) * outputAmplitudeY / scaleAmplitudeY;
			figure.Segments.Add(new LineSegment() { Point = new(x, y) });
		}

		return new PathGeometry() { Figures = { figure } };
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
