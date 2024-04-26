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
[TemplatePart(Name = VerticalGridLinesPathPartName, Type = typeof(Path))]
[TemplatePart(Name = MinMaxPathPartName, Type = typeof(Path))]
internal class LineChart : Control
{
	private const string LayoutGridPartName = "PART_LayoutGrid";
	private const string StrokePathPartName = "PART_StrokePath";
	private const string FillPathPartName = "PART_FillPath";
	private const string HorizontalGridLinesPathPartName = "PART_HorizontalGridLinesPath";
	private const string VerticalGridLinesPathPartName = "PART_VerticalGridLinesPath";
	private const string MinMaxPathPartName = "PART_MinMaxLinesPath";

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

	public Brush VerticalGridStroke
	{
		get => (Brush)GetValue(VerticalGridStrokeProperty);
		set => SetValue(VerticalGridStrokeProperty, value);
	}

	public static readonly DependencyProperty VerticalGridStrokeProperty = DependencyProperty.Register(nameof(VerticalGridStroke), typeof(Brush), typeof(LineChart), new PropertyMetadata(new SolidColorBrush()));

	public Brush MinMaxLineStroke
	{
		get => (Brush)GetValue(MinMaxLineStrokeProperty);
		set => SetValue(MinMaxLineStrokeProperty, value);
	}

	public static readonly DependencyProperty MinMaxLineStrokeProperty = DependencyProperty.Register(nameof(MinMaxLineStroke), typeof(Brush), typeof(LineChart), new PropertyMetadata(new SolidColorBrush()));

	private Path? _strokePath;
	private Path? _fillPath;
	private Path? _horizontalGridLinesPath;
	private Path? _verticalGridLinesPath;
	private Path? _minMaxLinesPath;
	private Grid? _layoutGrid;
	private readonly EventHandler _seriesDataChanged;

	public LineChart()
	{
		_seriesDataChanged = OnSeriesDataChanged;

		Loaded += static (s, e) => ((LineChart)s).OnLoaded(e);
		Unloaded += static (s, e) => ((LineChart)s).OnUnloaded(e);
		SizeChanged += static (s, e) => ((LineChart)s).OnSizeChanged(e);
	}

	private void OnLoaded(RoutedEventArgs e)
	{
		if (Series is { } series) series.Changed += _seriesDataChanged;
		RefreshChart();
	}

	private void OnUnloaded(RoutedEventArgs e)
	{
		if (Series is { } series) series.Changed -= _seriesDataChanged;
	}

	private void OnSizeChanged(SizeChangedEventArgs e) => RefreshChart();

	private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LineChart)d).OnSeriesChanged(e);

	private void OnSeriesChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue is ITimeSeries old) old.Changed -= _seriesDataChanged;
		if (IsLoaded)
		{
			if (e.NewValue is ITimeSeries @new) @new.Changed += _seriesDataChanged;
			RefreshChart();
		}
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
		_verticalGridLinesPath = GetTemplateChild(VerticalGridLinesPathPartName) as Path;
		_minMaxLinesPath = GetTemplateChild(MinMaxPathPartName) as Path;
		_layoutGrid = GetTemplateChild(LayoutGridPartName) as Grid;
		AttachParts();
		RefreshChart();
	}

	private void DetachParts()
	{
		if (_strokePath is not null) _strokePath.Data = null;
		if (_fillPath is not null) _fillPath.Data = null;
		if (_horizontalGridLinesPath is not null) _horizontalGridLinesPath.Data = null;
		if (_verticalGridLinesPath is not null) _verticalGridLinesPath.Data = null;
		if (_minMaxLinesPath is not null) _minMaxLinesPath.Data = null;
	}

	private void AttachParts()
	{
	}

	private void RefreshChart()
	{
		if (Series is null || ActualHeight == 0 || ActualWidth == 0)
		{
			SetData(_strokePath, null);
			SetData(_fillPath, null);
			SetData(_horizontalGridLinesPath, null);
			SetData(_verticalGridLinesPath, null);
			SetData(_minMaxLinesPath, null);
		}
		else
		{
			var (stroke, fill, horizontalGridLines, verticalGridLines, minMaxLines) = GenerateCurves
			(
				Series,
				ScaleYMinimum,
				ScaleYMaximum,
				_layoutGrid?.ActualWidth ?? ActualWidth,
				_layoutGrid?.ActualHeight ?? ActualHeight
			);
			SetData(_strokePath, stroke);
			SetData(_fillPath, fill);
			SetData(_horizontalGridLinesPath, horizontalGridLines);
			SetData(_verticalGridLinesPath, verticalGridLines);
			SetData(_minMaxLinesPath, minMaxLines);
		}
	}

	private void SetData(Path? path, Geometry? data)
	{
		if (path is null) return;
		path.Data = data;
	}

	private static (PathGeometry Stroke, PathGeometry Fill, PathGeometry HorizontalGridLines, PathGeometry VerticalGridLines, PathGeometry MinMaxLines) GenerateCurves(ITimeSeries series, double minValue, double maxValue, double outputWidth, double outputHeight)
	{
		for (int i = 0; i < series.Length; i++)
		{
			double value = series[i];
			minValue = Math.Min(value, minValue);
			maxValue = Math.Max(value, maxValue);
		}

		{
			if (series.MinimumReachedValue is double minReachedValue && minReachedValue < minValue) minValue = minReachedValue;
			if (series.MaximumReachedValue is double maxReachedValue && maxReachedValue > maxValue) maxValue = maxReachedValue;
		}

		// Anchor the scale to zero if necessary.
		if (maxValue < 0) maxValue = 0;
		if (minValue > 0) minValue = 0;

		// Force the chart to not be fully empty if the min and max are both zero. (result of previous adjustments)
		if (minValue == maxValue) maxValue = 1;

		var (scaleMinY, scaleMaxY, tickSpacingY) = NiceScale.Compute(minValue, maxValue);

		double scaleAmplitudeX = series.Length - 1;
		double scaleAmplitudeY = scaleMaxY - scaleMinY;
		int tickCount = (int)(scaleAmplitudeY / tickSpacingY) + 1;
		double outputAmplitudeX = outputWidth;
		double outputAmplitudeY = outputHeight;

		var point = new Point(0, outputAmplitudeY - (series[0] - scaleMinY) * outputAmplitudeY / scaleAmplitudeY);

		var outlineSegment = new PolyLineSegment();
		var outlineFigure = new PathFigure() { StartPoint = point, Segments = { outlineSegment } };
		var fillSegment = new PolyLineSegment { Points = { point } };
		var fillFigure = new PathFigure { StartPoint = new(0, outputAmplitudeY - -scaleMinY * outputAmplitudeY / scaleAmplitudeY), Segments = { fillSegment } };
		for (int j = 1; j < series.Length; j++)
		{
			double value = series[j];
			double x = j * outputAmplitudeX / scaleAmplitudeX;
			double y = outputAmplitudeY - (value - scaleMinY) * outputAmplitudeY / scaleAmplitudeY;
			point = new Point(x, y);
			outlineSegment.Points.Add(point);
			fillSegment.Points.Add(point);
		}
		fillSegment.Points.Add(new(outputAmplitudeX, fillFigure.StartPoint.Y));

		var horizontalGridLines = new PathGeometry();
		var verticalGridLines = new PathGeometry();

		for (int i = 0; i < tickCount; i++)
		{
			double y = i * tickSpacingY * outputAmplitudeY / scaleAmplitudeY;
			var figure = new PathFigure
			{
				StartPoint = new(0, y),
				Segments = { new LineSegment { Point = new(outputAmplitudeX, y) } }
			};
			horizontalGridLines.Figures.Add(figure);
		}

		// NB: Hardcode logic to display exactly 10 ticks, as we assume that datapoints are evenly spaced on a fixed time scale.
		var (tickSpacingX, tickOffsetX) = Math.DivRem((uint)series.Length, 10);
		for (int i = 0; i <= 10; i++)
		{
			double x = (int)(tickOffsetX + i * tickSpacingX) * outputAmplitudeX / scaleAmplitudeX;
			var figure = new PathFigure
			{
				StartPoint = new(x, 0),
				Segments = { new LineSegment { Point = new(x, outputAmplitudeY) } }
			};
			verticalGridLines.Figures.Add(figure);
		}

		var minMaxLines = new PathGeometry();

		{
			if (series.MinimumReachedValue is double minReachedValue)
			{
				double y = outputAmplitudeY - (minReachedValue - scaleMinY) * outputAmplitudeY / scaleAmplitudeY;
				var figure = new PathFigure
				{
					StartPoint = new(0, y),
					Segments = { new LineSegment { Point = new(outputAmplitudeX, y) } }
				};
				minMaxLines.Figures.Add(figure);
			}
			if (series.MaximumReachedValue is double maxReachedValue)
			{
				double y = outputAmplitudeY - (maxReachedValue - scaleMinY) * outputAmplitudeY / scaleAmplitudeY;
				var figure = new PathFigure
				{
					StartPoint = new(0, y),
					Segments = { new LineSegment { Point = new(outputAmplitudeX, y) } }
				};
				minMaxLines.Figures.Add(figure);
			}
		}

		return (new PathGeometry() { Figures = { outlineFigure } }, new PathGeometry() { Figures = { fillFigure } }, horizontalGridLines, verticalGridLines, minMaxLines);
	}

	protected override Size MeasureOverride(Size availableSize) => availableSize;
}
