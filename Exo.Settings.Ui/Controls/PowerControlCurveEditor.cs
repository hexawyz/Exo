using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Content;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Exo.Settings.Ui.Controls;

[TemplatePart(Name = LayoutGridPartName, Type = typeof(Grid))]
[TemplatePart(Name = HorizontalGridLinesPathPartName, Type = typeof(Path))]
[TemplatePart(Name = VerticalGridLinesPathPartName, Type = typeof(Path))]
[TemplatePart(Name = CurvePathPartName, Type = typeof(Path))]
[TemplatePart(Name = SymbolsPathPartName, Type = typeof(Path))]
[TemplatePart(Name = HorizontalTicksItemsRepeaterPartName, Type = typeof(ItemsRepeater))]
[TemplatePart(Name = VerticalTicksItemsRepeaterPartName, Type = typeof(ItemsRepeater))]
internal sealed partial class PowerControlCurveEditor : Control
{
	private const string LayoutGridPartName = "PART_LayoutGrid";
	private const string CurvePathPartName = "PART_CurvePath";
	private const string HorizontalGridLinesPathPartName = "PART_HorizontalGridLinesPath";
	private const string VerticalGridLinesPathPartName = "PART_VerticalGridLinesPath";
	private const string SymbolsPathPartName = "PART_SymbolsPath";
	private const string HorizontalTicksItemsRepeaterPartName = "PART_HorizontalTicksItemsRepeater";
	private const string VerticalTicksItemsRepeaterPartName = "PART_VerticalTicksItemsRepeater";

	private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		=> ((PowerControlCurveEditor)d).OnPropertyChanged(e);

	private Path? _curvePath;
	private Path? _horizontalGridLinesPath;
	private Path? _verticalGridLinesPath;
	private ItemsRepeater? _horizontalTickItemsRepeater;
	private ItemsRepeater? _verticalTickItemsRepeater;
	private Grid? _layoutGrid;
	private readonly PathGeometry _curvePathGeometry;
	private readonly GeometryGroup _symbolsGeometryGroup;
	private readonly NotifyCollectionChangedEventHandler _pointsCollectionChanged;
	private readonly ObservableCollection<double> _horizontalTicks;
	private readonly ObservableCollection<double> _verticalTicks;

	public PowerControlCurveEditor()
	{
		_pointsCollectionChanged = OnPointsCollectionChanged;
		_horizontalTicks = new();
		_verticalTicks = new() { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 0 };

		_curvePathGeometry = new();
		_symbolsGeometryGroup = new();

		Loaded += static (s, e) => ((PowerControlCurveEditor)s).OnLoaded(e);
		Unloaded += static (s, e) => ((PowerControlCurveEditor)s).OnUnloaded(e);
		SizeChanged += static (s, e) => ((PowerControlCurveEditor)s).OnSizeChanged(e);
	}

	private void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.Property == PointsProperty) OnPointsChanged(e);
	}

	private void OnLoaded(RoutedEventArgs e)
	{
		if (Points is INotifyCollectionChanged points) points.CollectionChanged += _pointsCollectionChanged;
		RefreshChart();
	}

	private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
	}

	private void OnUnloaded(RoutedEventArgs e)
	{
		if (Points is INotifyCollectionChanged points) points.CollectionChanged -= _pointsCollectionChanged;
	}

	private void OnSizeChanged(SizeChangedEventArgs e) => RefreshChart();

	private void OnPointsChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged += _pointsCollectionChanged;
		if (IsLoaded)
		{
			if (e.NewValue is INotifyCollectionChanged @new) @new.CollectionChanged += _pointsCollectionChanged;
			RefreshChart();
		}
	}

	protected override void OnApplyTemplate()
	{
		DetachParts();
		_layoutGrid = GetTemplateChild(LayoutGridPartName) as Grid;
		_horizontalGridLinesPath = GetTemplateChild(HorizontalGridLinesPathPartName) as Path;
		_verticalGridLinesPath = GetTemplateChild(VerticalGridLinesPathPartName) as Path;
		_curvePath = GetTemplateChild(CurvePathPartName) as Path;
		_horizontalTickItemsRepeater = GetTemplateChild(HorizontalTicksItemsRepeaterPartName) as ItemsRepeater;
		_verticalTickItemsRepeater = GetTemplateChild(VerticalTicksItemsRepeaterPartName) as ItemsRepeater;
		AttachParts();
		RefreshChart();
	}

	private void DetachParts()
	{
		if (_curvePath is not null) _curvePath.Data = null;
		if (_horizontalGridLinesPath is not null) _horizontalGridLinesPath.Data = null;
		if (_verticalGridLinesPath is not null) _verticalGridLinesPath.Data = null;
		if (_verticalTickItemsRepeater is not null) _verticalTickItemsRepeater.ItemsSource = _verticalTicks;
		if (_horizontalTickItemsRepeater is not null) _horizontalTickItemsRepeater.ItemsSource = null;
	}

	private void AttachParts()
	{
		if (_verticalTickItemsRepeater is not null) _verticalTickItemsRepeater.ItemsSource = _verticalTicks;
		if (_horizontalTickItemsRepeater is not null) _horizontalTickItemsRepeater.ItemsSource = _horizontalTicks;
	}

	private void RefreshChart()
	{
		//if (Points is null || ActualHeight == 0 || ActualWidth == 0)
		//{
		//	SetData(_strokePath, null);
		//	SetData(_fillPath, null);
		//	SetData(_horizontalGridLinesPath, null);
		//	SetData(_verticalGridLinesPath, null);
		//	SetData(_minMaxLinesPath, null);
		//}
		//else
		//{
		//	var (stroke, fill, horizontalGridLines, verticalGridLines, minMaxLines) = GenerateCurves
		//	(
		//		Series,
		//		ScaleYMinimum,
		//		ScaleYMaximum,
		//		_layoutGrid?.ActualWidth ?? ActualWidth,
		//		_layoutGrid?.ActualHeight ?? ActualHeight
		//	);
		//	SetData(_strokePath, stroke);
		//	SetData(_fillPath, fill);
		//	SetData(_horizontalGridLinesPath, horizontalGridLines);
		//	SetData(_verticalGridLinesPath, verticalGridLines);
		//	SetData(_minMaxLinesPath, minMaxLines);
		//}
	}

	private void SetData(Path? path, Geometry? data)
	{
		if (path is null) return;
		path.Data = data;
	}

	//private static (PathGeometry Stroke, PathGeometry Fill, PathGeometry HorizontalGridLines, PathGeometry VerticalGridLines, PathGeometry MinMaxLines) GenerateCurves(ITimeSeries series, double minValue, double maxValue, double outputWidth, double outputHeight)
	//{
	//	for (int i = 0; i < series.Length; i++)
	//	{
	//		double value = series[i];
	//		minValue = Math.Min(value, minValue);
	//		maxValue = Math.Max(value, maxValue);
	//	}

	//	{
	//		if (series.MinimumReachedValue is double minReachedValue && minReachedValue < minValue) minValue = minReachedValue;
	//		if (series.MaximumReachedValue is double maxReachedValue && maxReachedValue > maxValue) maxValue = maxReachedValue;
	//	}

	//	// Anchor the scale to zero if necessary.
	//	if (maxValue < 0) maxValue = 0;
	//	if (minValue > 0) minValue = 0;

	//	// Force the chart to not be fully empty if the min and max are both zero. (result of previous adjustments)
	//	if (minValue == maxValue) maxValue = 1;

	//	var (scaleMinY, scaleMaxY, tickSpacingY) = NiceScale.Compute(minValue, maxValue);

	//	double scaleAmplitudeX = series.Length - 1;
	//	double scaleAmplitudeY = scaleMaxY - scaleMinY;
	//	int tickCount = (int)(scaleAmplitudeY / tickSpacingY) + 1;
	//	double outputAmplitudeX = outputWidth;
	//	double outputAmplitudeY = outputHeight;

	//	var point = new Point(0, outputAmplitudeY - (series[0] - scaleMinY) * outputAmplitudeY / scaleAmplitudeY);

	//	var outlineSegment = new PolyLineSegment();
	//	var outlineFigure = new PathFigure() { StartPoint = point, Segments = { outlineSegment } };
	//	var fillSegment = new PolyLineSegment { Points = { point } };
	//	var fillFigure = new PathFigure { StartPoint = new(0, outputAmplitudeY - -scaleMinY * outputAmplitudeY / scaleAmplitudeY), Segments = { fillSegment } };
	//	for (int j = 1; j < series.Length; j++)
	//	{
	//		double value = series[j];
	//		double x = j * outputAmplitudeX / scaleAmplitudeX;
	//		double y = outputAmplitudeY - (value - scaleMinY) * outputAmplitudeY / scaleAmplitudeY;
	//		point = new Point(x, y);
	//		outlineSegment.Points.Add(point);
	//		fillSegment.Points.Add(point);
	//	}
	//	fillSegment.Points.Add(new(outputAmplitudeX, fillFigure.StartPoint.Y));

	//	var horizontalGridLines = new PathGeometry();
	//	var verticalGridLines = new PathGeometry();

	//	for (int i = 0; i < tickCount; i++)
	//	{
	//		double y = i * tickSpacingY * outputAmplitudeY / scaleAmplitudeY;
	//		var figure = new PathFigure
	//		{
	//			StartPoint = new(0, y),
	//			Segments = { new LineSegment { Point = new(outputAmplitudeX, y) } }
	//		};
	//		horizontalGridLines.Figures.Add(figure);
	//	}

	//	// NB: Hardcode logic to display exactly 10 ticks, as we assume that datapoints are evenly spaced on a fixed time scale.
	//	var (tickSpacingX, tickOffsetX) = Math.DivRem((uint)series.Length, 10);
	//	for (int i = 0; i <= 10; i++)
	//	{
	//		double x = (int)(tickOffsetX + i * tickSpacingX) * outputAmplitudeX / scaleAmplitudeX;
	//		var figure = new PathFigure
	//		{
	//			StartPoint = new(x, 0),
	//			Segments = { new LineSegment { Point = new(x, outputAmplitudeY) } }
	//		};
	//		verticalGridLines.Figures.Add(figure);
	//	}

	//	var minMaxLines = new PathGeometry();

	//	{
	//		if (series.MinimumReachedValue is double minReachedValue)
	//		{
	//			double y = outputAmplitudeY - (minReachedValue - scaleMinY) * outputAmplitudeY / scaleAmplitudeY;
	//			var figure = new PathFigure
	//			{
	//				StartPoint = new(0, y),
	//				Segments = { new LineSegment { Point = new(outputAmplitudeX, y) } }
	//			};
	//			minMaxLines.Figures.Add(figure);
	//		}
	//		if (series.MaximumReachedValue is double maxReachedValue)
	//		{
	//			double y = outputAmplitudeY - (maxReachedValue - scaleMinY) * outputAmplitudeY / scaleAmplitudeY;
	//			var figure = new PathFigure
	//			{
	//				StartPoint = new(0, y),
	//				Segments = { new LineSegment { Point = new(outputAmplitudeX, y) } }
	//			};
	//			minMaxLines.Figures.Add(figure);
	//		}
	//	}

	//	return (new PathGeometry() { Figures = { outlineFigure } }, new PathGeometry() { Figures = { fillFigure } }, horizontalGridLines, verticalGridLines, minMaxLines);
	//}
}
