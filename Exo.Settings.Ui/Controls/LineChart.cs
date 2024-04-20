using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Exo.Settings.Ui.Controls;

[TemplatePart(Name = LayoutGridPartName, Type = typeof(Grid))]
[TemplatePart(Name = StrokePathPartName, Type = typeof(Path))]
[TemplatePart(Name = FillPathPartName, Type = typeof(Path))]
[TemplatePart(Name = HorizontalGridLinesPathPartName, Type = typeof(Path))]
internal class LineChart : Control
{
	private const string LayoutGridPartName = "PART_LayoutGrid";
	private const string StrokePathPartName = "PART_StrokePath";
	private const string FillPathPartName = "PART_FillPath";
	private const string HorizontalGridLinesPathPartName = "PART_HorizontalGridLinesPath";

	public ITimeSeries? Series
	{
		get => (ITimeSeries)GetValue(SeriesProperty);
		set => SetValue(SeriesProperty, value);
	}

	public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(ITimeSeries), typeof(LineChart), new PropertyMetadata(null, OnSeriesChanged));

	// TODO: Should be nullable once the WinUI bug is fixed.
	public double ScaleYMinimum
	{
		get => (double)GetValue(ScaleYMinimumProperty);
		set => SetValue(ScaleYMinimumProperty, value);
	}

	public static readonly DependencyProperty ScaleYMinimumProperty = DependencyProperty.Register(nameof(ScaleYMinimum), typeof(double), typeof(LineChart), new PropertyMetadata(double.PositiveInfinity, OnScaleChanged));

	// TODO: Should be nullable once the WinUI bug is fixed.
	public double ScaleYMaximum
	{
		get => (double)GetValue(ScaleYMaximumProperty);
		set => SetValue(ScaleYMaximumProperty, value);
	}

	public static readonly DependencyProperty ScaleYMaximumProperty = DependencyProperty.Register(nameof(ScaleYMaximum), typeof(double), typeof(LineChart), new PropertyMetadata(double.NegativeInfinity, OnScaleChanged));

	public Brush AreaFill
	{
		get => (Brush)GetValue(AreaFillProperty);
		set => SetValue(AreaFillProperty, value);
	}

	public static readonly DependencyProperty AreaFillProperty = DependencyProperty.Register(nameof(AreaFill), typeof(Brush), typeof(LineChart), new PropertyMetadata(new SolidColorBrush()));

	public double AreaOpacity
	{
		get => (double)GetValue(AreaOpacityProperty);
		set => SetValue(AreaOpacityProperty, value);
	}

	public static readonly DependencyProperty AreaOpacityProperty = DependencyProperty.Register(nameof(AreaOpacity), typeof(double), typeof(LineChart), new PropertyMetadata(1d));

	public Brush Stroke
	{
		get => (Brush)GetValue(StrokeProperty);
		set => SetValue(StrokeProperty, value);
	}

	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(LineChart), new PropertyMetadata(new SolidColorBrush()));

	public double StrokeThickness
	{
		get => (double)GetValue(StrokeThicknessProperty);
		set => SetValue(StrokeThicknessProperty, value);
	}

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(LineChart), new PropertyMetadata(1d));

	public PenLineJoin StrokeLineJoin
	{
		get => (PenLineJoin)GetValue(StrokeLineJoinProperty);
		set => SetValue(StrokeLineJoinProperty, value);
	}

	public static readonly DependencyProperty StrokeLineJoinProperty = DependencyProperty.Register(nameof(StrokeLineJoin), typeof(PenLineJoin), typeof(LineChart), new PropertyMetadata(PenLineJoin.Round));

	public Brush HorizontalGridStroke
	{
		get => (Brush)GetValue(HorizontalGridStrokeProperty);
		set => SetValue(HorizontalGridStrokeProperty, value);
	}

	public static readonly DependencyProperty HorizontalGridStrokeProperty = DependencyProperty.Register(nameof(HorizontalGridStroke), typeof(Brush), typeof(LineChart), new PropertyMetadata(new SolidColorBrush()));

	private Path? _strokePath;
	private Path? _fillPath;
	private Path? _horizontalGridLinesPath;
	private Grid? _layoutGrid;
	private readonly EventHandler _seriesDataChanged;

	public LineChart()
	{
		_seriesDataChanged = OnSeriesDataChanged;
	}

	private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LineChart)d).OnSeriesChanged(e);

	private void OnSeriesChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue is ITimeSeries old) old.Changed -= _seriesDataChanged;
		if (e.NewValue is ITimeSeries @new) @new.Changed += _seriesDataChanged;
		RefreshChart();
	}

	private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LineChart)d).OnScaleChanged(e);

	private void OnScaleChanged(DependencyPropertyChangedEventArgs e) => RefreshChart();

	private void OnSeriesDataChanged(object? sender, EventArgs e) => RefreshChart();

	protected override void OnApplyTemplate()
	{
		DetachParts();
		_strokePath = GetTemplateChild(StrokePathPartName) as Path;
		_fillPath = GetTemplateChild(FillPathPartName) as Path;
		_horizontalGridLinesPath = GetTemplateChild(HorizontalGridLinesPathPartName) as Path;
		_layoutGrid = GetTemplateChild(LayoutGridPartName) as Grid;
		AttachParts();
		RefreshChart();
	}

	private void DetachParts()
	{
		if (_strokePath is not null) _strokePath.Data = null;
		if (_fillPath is not null) _fillPath.Data = null;
		if (_horizontalGridLinesPath is not null) _horizontalGridLinesPath.Data = null;
	}

	private void AttachParts()
	{
	}

	private void RefreshChart()
	{
		if (Series is null)
		{
			if (_strokePath is { }) _strokePath.Data = null;
			if (_fillPath is { }) _fillPath.Data = null;
		}
		else
		{
			var (stroke, fill, horizontalGridLines) = GenerateCurves
			(
				Series,
				ScaleYMinimum,
				ScaleYMaximum,
				_layoutGrid?.ActualWidth ?? ActualWidth,
				_layoutGrid?.ActualHeight ?? ActualHeight
			);
			if (_strokePath is { }) _strokePath.Data = stroke;
			if (_fillPath is { }) _fillPath.Data = fill;
			if (_horizontalGridLinesPath is { }) _horizontalGridLinesPath.Data = horizontalGridLines;
		}
	}

	private (PathGeometry Stroke, PathGeometry Fill, GeometryGroup HorizontalGridLines) GenerateCurves(ITimeSeries series, double minValue, double maxValue, double outputWidth, double outputHeight)
	{
		// NB: This is very rough and WIP.
		// It should probably be ported to a dedicated chart drawing component afterwards.

		for (int i = 0; i < series.Length; i++)
		{
			double value = series[i];
			minValue = Math.Min(value, minValue);
			maxValue = Math.Max(value, maxValue);
		}

		// Anchor the scale to zero if necessary.
		if (maxValue < 0) maxValue = 0;
		if (minValue > 0) minValue = 0;

		// Force the chart to not be fully empty if the min and max are both zero. (result of previous adjustments)
		if (minValue == maxValue) maxValue = 1;

		var (scaleMin, scaleMax, tickSpacing) = NiceScale.Compute(minValue, maxValue);

		double scaleAmplitudeX = series.Length - 1;
		double scaleAmplitudeY = scaleMax - scaleMin;
		double outputAmplitudeX = outputWidth;
		double outputAmplitudeY = outputHeight;

		var outlineFigure = new PathFigure();
		var fillFigure = new PathFigure();

		fillFigure.StartPoint = new(0, outputAmplitudeY - -scaleMin * outputAmplitudeY / scaleAmplitudeY);

		var point = new Point(0, outputAmplitudeY - (series[0] - scaleMin) * outputAmplitudeY / scaleAmplitudeY);
		outlineFigure.StartPoint = point;
		fillFigure.Segments.Add(new LineSegment { Point = point });
		for (int j = 1; j < series.Length; j++)
		{
			double value = series[j];
			double x = j * outputAmplitudeX / scaleAmplitudeX;
			double y = outputAmplitudeY - (value - scaleMin) * outputAmplitudeY / scaleAmplitudeY;
			point = new Point(x, y);
			outlineFigure.Segments.Add(new LineSegment() { Point = point });
			fillFigure.Segments.Add(new LineSegment() { Point = point });
		}

		fillFigure.Segments.Add(new LineSegment() { Point = new(outputAmplitudeX, fillFigure.StartPoint.Y) });

		var horizontalGridLines = new GeometryGroup();

		for (double lineY = scaleMin + tickSpacing; lineY < scaleMax; lineY += tickSpacing)
		{
			double y = outputAmplitudeY - lineY * outputAmplitudeY / scaleAmplitudeY;
			horizontalGridLines.Children.Add(new LineGeometry() { StartPoint = new(0, y), EndPoint = new(outputAmplitudeX, y) });
		}

		return (new PathGeometry() { Figures = { outlineFigure } }, new PathGeometry() { Figures = { fillFigure } }, horizontalGridLines);
	}

	protected override Size MeasureOverride(Size availableSize) => availableSize;
}