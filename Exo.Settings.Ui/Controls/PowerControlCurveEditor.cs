using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using Microsoft.UI.Content;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
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
[TemplatePart(Name = PowerValueToolTipPartName, Type = typeof(ToolTip))]
internal sealed partial class PowerControlCurveEditor : Control
{
	private const string LayoutGridPartName = "PART_LayoutGrid";
	private const string CurvePathPartName = "PART_CurvePath";
	private const string HorizontalGridLinesPathPartName = "PART_HorizontalGridLinesPath";
	private const string VerticalGridLinesPathPartName = "PART_VerticalGridLinesPath";
	private const string SymbolsPathPartName = "PART_SymbolsPath";
	private const string HorizontalTicksItemsRepeaterPartName = "PART_HorizontalTicksItemsRepeater";
	private const string VerticalTicksItemsRepeaterPartName = "PART_VerticalTicksItemsRepeater";
	private const string PowerValueToolTipPartName = "PART_PowerValueToolTip";

	private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		=> ((PowerControlCurveEditor)d).OnPropertyChanged(e);

	private Path? _curvePath;
	private Path? _symbolsPath;
	private Path? _horizontalGridLinesPath;
	private Path? _verticalGridLinesPath;
	private ToolTip? _powerValueToolTip;
	private ItemsRepeater? _horizontalTickItemsRepeater;
	private ItemsRepeater? _verticalTickItemsRepeater;
	private Grid? _layoutGrid;
	private readonly PathGeometry _verticalGridLinesPathGeometry;
	private readonly PathGeometry _horizontalGridLinesPathGeometry;
	private readonly PathGeometry _curvePathGeometry;
	private readonly GeometryGroup _symbolsGeometryGroup;
	private readonly TranslateTransform _symbolsTranslateTransform;
	private LinearScale? _horizontalScale;
	private LinearScale? _verticalScale;
	private double[] _horizontalTicks;
	private readonly double[] _verticalTicks;

	private Pointer? _capturedPointer;
	private int _draggedPointIndex;
	private object? _draggedPointCurrentInputValue;
	private double _draggedPointRelativePower;
	private byte _draggedPointCurrentPower;

	private readonly NotifyCollectionChangedEventHandler _pointsCollectionChanged;
	private readonly PointerEventHandler _symbolsPathPointerPressed;
	private readonly PointerEventHandler _symbolsPathPointerMoved;
	private readonly PointerEventHandler _symbolsPathPointerReleased;
	private readonly PointerEventHandler _symbolsPathPointerCanceled;
	private readonly PointerEventHandler _symbolsPathPointerCaptureLost;

	public PowerControlCurveEditor()
	{
		_draggedPointIndex = -1;

		_pointsCollectionChanged = OnPointsCollectionChanged;
		_symbolsPathPointerPressed = OnSymbolsPathPointerPressed;
		_symbolsPathPointerMoved = OnSymbolsPathPointerMoved;
		_symbolsPathPointerReleased = OnSymbolsPathPointerReleased;
		_symbolsPathPointerCanceled = OnSymbolsPathPointerCanceled;
		_symbolsPathPointerCaptureLost = OnSymbolsPathPointerCaptureLost;
		_horizontalTicks = [];
		_verticalTicks = [100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 0];

		_verticalGridLinesPathGeometry = new();
		_horizontalGridLinesPathGeometry = new();
		_curvePathGeometry = new();
		_symbolsGeometryGroup = new();
		_symbolsTranslateTransform = new();

		Loaded += static (s, e) => ((PowerControlCurveEditor)s).OnLoaded(e);
		Unloaded += static (s, e) => ((PowerControlCurveEditor)s).OnUnloaded(e);
		SizeChanged += static (s, e) => ((PowerControlCurveEditor)s).OnSizeChanged(e);
	}

	private void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.Property == PointsProperty) OnPointsChanged(e);
		if (e.Property == SymbolRadiusProperty) UpdateSymbolsTranslation();
		RefreshCurve();
	}

	private void OnLoaded(RoutedEventArgs e)
	{
		if (Points is INotifyCollectionChanged points) points.CollectionChanged += _pointsCollectionChanged;
		RefreshScales(true);
		RefreshCurve();
	}

	private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (_capturedPointer is not null && _symbolsPath is not null)
		{
			_symbolsPath.ReleasePointerCapture(_capturedPointer);
			_capturedPointer = null;
			_draggedPointIndex = -1;
		}
	}

	private void OnUnloaded(RoutedEventArgs e)
	{
		if (Points is INotifyCollectionChanged points) points.CollectionChanged -= _pointsCollectionChanged;
	}

	private void OnSizeChanged(SizeChangedEventArgs e)
	{
		RefreshScales(true);
		RefreshCurve();
	}

	private void OnPointsChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged += _pointsCollectionChanged;
		if (IsLoaded)
		{
			if (e.NewValue is INotifyCollectionChanged @new) @new.CollectionChanged += _pointsCollectionChanged;
			RefreshScales(false);
			RefreshCurve();
		}
	}

	protected override void OnApplyTemplate()
	{
		DetachParts();
		_layoutGrid = GetTemplateChild(LayoutGridPartName) as Grid;
		_horizontalGridLinesPath = GetTemplateChild(HorizontalGridLinesPathPartName) as Path;
		_verticalGridLinesPath = GetTemplateChild(VerticalGridLinesPathPartName) as Path;
		_curvePath = GetTemplateChild(CurvePathPartName) as Path;
		_symbolsPath = GetTemplateChild(SymbolsPathPartName) as Path;
		_horizontalTickItemsRepeater = GetTemplateChild(HorizontalTicksItemsRepeaterPartName) as ItemsRepeater;
		_verticalTickItemsRepeater = GetTemplateChild(VerticalTicksItemsRepeaterPartName) as ItemsRepeater;
		_powerValueToolTip = GetTemplateChild(PowerValueToolTipPartName) as ToolTip;
		AttachParts();
		RefreshScales(true);
		RefreshCurve();
	}

	private void DetachParts()
	{
		_horizontalScale = null;
		_verticalScale = null;
		_horizontalTicks = [];
		if (_powerValueToolTip is not null)
		{
			_powerValueToolTip.ClearValue(ToolTip.IsOpenProperty);
			_powerValueToolTip.ClearValue(IsEnabledProperty);
			_powerValueToolTip.ClearValue(ContentControl.ContentProperty);
		}
		SetData(_curvePath, null);
		SetData(_symbolsPath, null);
		if (_symbolsPath is not null)
		{
			_symbolsPath.RenderTransform = null;
			_symbolsPath.PointerPressed -= _symbolsPathPointerPressed;
			_symbolsPath.PointerMoved -= _symbolsPathPointerMoved;
			_symbolsPath.PointerReleased -= _symbolsPathPointerReleased;
			_symbolsPath.PointerCanceled -= _symbolsPathPointerCanceled;
			_symbolsPath.PointerCaptureLost -= _symbolsPathPointerCaptureLost;
		}
		SetData(_verticalGridLinesPath, null);
		SetData(_horizontalGridLinesPath, null);
		if (_verticalTickItemsRepeater is not null) _verticalTickItemsRepeater.ItemsSource = null;
		if (_horizontalTickItemsRepeater is not null) _horizontalTickItemsRepeater.ItemsSource = null;
	}

	private void AttachParts()
	{
		SetData(_horizontalGridLinesPath, _horizontalGridLinesPathGeometry);
		SetData(_verticalGridLinesPath, _verticalGridLinesPathGeometry);
		SetData(_curvePath, _curvePathGeometry);
		SetData(_symbolsPath, _symbolsGeometryGroup);
		if (_symbolsPath is not null)
		{
			_symbolsPath.RenderTransform = _symbolsTranslateTransform;
			_symbolsPath.PointerPressed += _symbolsPathPointerPressed;
			_symbolsPath.PointerMoved += _symbolsPathPointerMoved;
			_symbolsPath.PointerReleased += _symbolsPathPointerReleased;
			_symbolsPath.PointerCanceled += _symbolsPathPointerCanceled;
			_symbolsPath.PointerCaptureLost += _symbolsPathPointerCaptureLost;
		}
		if (_verticalTickItemsRepeater is not null) _verticalTickItemsRepeater.ItemsSource = _verticalTicks;
		if (_horizontalTickItemsRepeater is not null) _horizontalTickItemsRepeater.ItemsSource = _horizontalTicks;
	}

	private void UpdateSymbolsTranslation()
	{
		double radius = SymbolRadius;
		_symbolsTranslateTransform.X = radius;
		_symbolsTranslateTransform.Y = radius;
	}

	private void RefreshScales(bool isResize)
	{
		double width = _layoutGrid?.ActualWidth ?? 1;
		double height = _layoutGrid?.ActualHeight ?? 1;
		double minX = GetOrDefault(MinimumInputValue, double.PositiveInfinity);
		double maxX = GetOrDefault(MaximumInputValue, double.NegativeInfinity);
		double curveThickness = CurveStrokeThickness;
		LinearScale horizontalScale;
		LinearScale verticalScale;
		double[] horizontalTicks;

		(horizontalScale, verticalScale, horizontalTicks) = GenerateScales(Points, minX, maxX, width, height, curveThickness);

		_horizontalScale = horizontalScale;
		_verticalScale = verticalScale;
		_horizontalTicks = horizontalTicks;

		if (isResize)
		{
			_horizontalGridLinesPathGeometry.Figures.Clear();
			for (int i = 0; i < _verticalTicks.Length; i++)
			{
				double y = verticalScale[_verticalTicks[i]];
				_horizontalGridLinesPathGeometry.Figures.Add(new PathFigure { StartPoint = new() { X = 0, Y = y }, Segments = { new LineSegment() { Point = new() { X = width - 1, Y = y } } } });
			}
		}

		_verticalGridLinesPathGeometry.Figures.Clear();
		for (int i = 0; i < _horizontalTicks.Length; i++)
		{
			double x = horizontalScale[_horizontalTicks[i]];
			_verticalGridLinesPathGeometry.Figures.Add(new PathFigure { StartPoint = new() { X = x, Y = 0 }, Segments = { new LineSegment() { Point = new() { X = x, Y = height - 1 } } } });
		}

		if (_verticalTickItemsRepeater is not null) _verticalTickItemsRepeater.ItemsSource = _verticalTicks;
		if (_horizontalTickItemsRepeater is not null) _horizontalTickItemsRepeater.ItemsSource = _horizontalTicks;
	}

	private void RefreshCurve()
	{
		if (_horizontalScale is null || _verticalScale is null) return;

		byte minimumPower = MinimumPower;
		bool canSwitchOff = CanSwitchOff;
		double symbolRadius = SymbolRadius;
		if (_capturedPointer is null)
		{
			RedrawChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, Points, minimumPower, canSwitchOff, symbolRadius);
		}
		else
		{
			UpdateChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, Points, _draggedPointIndex, _draggedPointCurrentInputValue, _draggedPointCurrentPower, minimumPower, canSwitchOff, symbolRadius);
		}
	}

	private static void SetData(Path? path, Geometry? data)
	{
		if (path is null) return;
		path.Data = data;
	}

	private static double GetOrDefault(object? value, double defaultValue)
		=> value is not null ? Convert.ToDouble(value) : default;

	private static (LinearScale HorizontalScale, LinearScale VerticalScale, double[] HorizontalTicks) GenerateScales<T>(IList<IDataPoint<T, byte>> points, double minX, double maxX, double outputWidth, double outputHeight, double lineThickness)
		where T : struct, INumber<T>
	{
		if (points is not null)
		{
			for (int i = 0; i < points.Count; i++)
			{
				var dataPoint = points[i];
				double x = double.CreateChecked(dataPoint.X);
				minX = Math.Min(x, minX);
				maxX = Math.Max(x, maxX);
			}
		}

		return GenerateScales(minX, maxX, outputWidth, outputHeight, lineThickness);
	}

	private static (LinearScale HorizontalScale, LinearScale VerticalScale, double[] HorizontalTicks) GenerateScales(object? points, double minX, double maxX, double outputWidth, double outputHeight, double lineThickness)
		=> points switch
		{
			IList<IDataPoint<int, byte>> pointsInt32 => GenerateScales(pointsInt32, minX, maxX, outputWidth, outputHeight, lineThickness),
			IList<IDataPoint<uint, byte>> pointsUInt32 => GenerateScales(pointsUInt32, minX, maxX, outputWidth, outputHeight, lineThickness),
			IList<IDataPoint<long, byte>> pointsInt64 => GenerateScales(pointsInt64, minX, maxX, outputWidth, outputHeight, lineThickness),
			IList<IDataPoint<ulong, byte>> pointsUInt64 => GenerateScales(pointsUInt64, minX, maxX, outputWidth, outputHeight, lineThickness),
			IList<IDataPoint<float, byte>> pointsSingle => GenerateScales(pointsSingle, minX, maxX, outputWidth, outputHeight, lineThickness),
			IList<IDataPoint<double, byte>> pointsDouble => GenerateScales(pointsDouble, minX, maxX, outputWidth, outputHeight, lineThickness),
			_ => GenerateScales(minX, maxX, outputWidth, outputHeight, lineThickness),
		};

	private static (LinearScale HorizontalScale, LinearScale VerticalScale, double[] HorizontalTicks) GenerateScales(double minX, double maxX, double outputWidth, double outputHeight, double lineThickness)
	{
		// Anchor the scale to zero if necessary.
		if (maxX < 0) maxX = 0;
		if (minX > 0) minX = 0;

		// Force the chart to not be fully empty if the min and max are both zero. (result of previous adjustments)
		if (minX == maxX) maxX = 1;

		var (scaleMinX, scaleMaxX, tickSpacingX) = NiceScale.Compute(minX, maxX);

		double t = 0.5 * lineThickness;
		var horizontalScale = new LinearScale(scaleMinX, scaleMaxX, t, outputWidth - t);
		var verticalScale = new LinearScale(0, 100, outputHeight - t, t);

		int tickCount = (int)(horizontalScale.InputAmplitude / tickSpacingX) + 1;

		var ticks = new double[tickCount];
		for (int i = 0; i < ticks.Length; i++)
		{
			ticks[i] = scaleMinX + i * tickSpacingX;
		}

		return (horizontalScale, verticalScale, ticks);
	}

	private static void RedrawChart<T>
	(
		LinearScale horizontalScale,
		LinearScale verticalScale,
		PathGeometry curvePathGeometry,
		GeometryGroup symbolsGeometryGroup,
		IList<IDataPoint<T, byte>> points,
		byte minimumPower,
		bool canSwitchOff,
		double symbolRadius
	)
		where T : INumber<T>
	{
		PathFigure curveFigure;
		if (curvePathGeometry.Figures.Count == 0)
		{
			curvePathGeometry.Figures.Add(curveFigure = new());
		}
		else
		{
			(curveFigure = curvePathGeometry.Figures[0]).Segments.Clear();
		}

		symbolsGeometryGroup.Children.Clear();

		if (points.Count == 0) return;

		IDataPoint<T, byte> point = points[0];
		double x = horizontalScale[double.CreateChecked(point.X)];
		double y = verticalScale[point.Y];
		// A hard step needs to be drawn if the curve starts at 0 and goes to its minimum value that is not zero afterwards.
		bool isStepRequired = canSwitchOff && minimumPower > 0;
		// The first point gets a special treatment, as we always want the curve to be anchored to the left side.
		// The default value in that case depends on the settings. It will be zero if switchable to off, or 
		if (double.CreateChecked(point.X) > horizontalScale.InputMinimum)
		{
			if (isStepRequired)
			{
				curveFigure.StartPoint = new() { X = horizontalScale.OutputMinimum, Y = verticalScale.OutputMinimum };
				curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = verticalScale.OutputMinimum } });
				if (point.Y != 0)
				{
					curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = y } });
					isStepRequired = false;
				}
			}
			else
			{
				curveFigure.StartPoint = new() { X = horizontalScale.OutputMinimum, Y = verticalScale[minimumPower] };
				curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = y } });
			}
		}
		else
		{
			curveFigure.StartPoint = new() { X = x, Y = y };
			isStepRequired &= point.Y == 0;
		}

		symbolsGeometryGroup.Children.Add(new EllipseGeometry() { Center = new() { X = x, Y = y }, RadiusX = symbolRadius, RadiusY = symbolRadius });

		for (int i = 1; i < points.Count; i++)
		{
			point = points[i];
			x = horizontalScale[double.CreateChecked(point.X)];
			if (isStepRequired && point.Y != 0)
			{
				curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = y } });
				isStepRequired = false;
			}
			y = verticalScale[point.Y];
			curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = y } });
			symbolsGeometryGroup.Children.Add(new EllipseGeometry() { Center = new() { X = x, Y = y }, RadiusX = symbolRadius, RadiusY = symbolRadius });
		}

		if (double.CreateChecked(point.X) < horizontalScale.InputMaximum)
		{
			curveFigure.Segments.Add(new LineSegment() { Point = new() { X = horizontalScale.OutputMaximum, Y = y } });
		}
	}

	private static void RedrawChart
	(
		LinearScale horizontalScale,
		LinearScale verticalScale,
		PathGeometry curvePathGeometry,
		GeometryGroup symbolsGeometryGroup,
		object? points,
		byte minimumPower,
		bool canSwitchOff,
		double symbolRadius
	)
	{
		switch (points)
		{
		case IList<IDataPoint<int, byte>> pointsInt32: RedrawChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsInt32, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<uint, byte>> pointsUInt32: RedrawChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsUInt32, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<long, byte>> pointsInt64: RedrawChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsInt64, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<ulong, byte>> pointsUInt64: RedrawChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsUInt64, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<float, byte>> pointsSingle: RedrawChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsSingle, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<double, byte>> pointsDouble: RedrawChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsDouble, minimumPower, canSwitchOff, symbolRadius); break;
		default: return;
		}
	}

	// This function similar to RedrawChart will update the chart graphics based on the current change.
	// Hopefully, updating the already existing graphics should be less costly than recreating everything (which we would otherwise do in RedrawChart)
	// But more importantly, we only want to update the actual data points once the change is committed.
	private static void UpdateChart<T>
	(
		LinearScale horizontalScale,
		LinearScale verticalScale,
		PathGeometry curvePathGeometry,
		GeometryGroup symbolsGeometryGroup,
		IList<IDataPoint<T, byte>> points,
		int draggedPointIndex,
		T draggedPointInputValue,
		byte draggedPointPower,
		byte minimumPower,
		bool canSwitchOff,
		double symbolRadius
	)
		where T : INumber<T>
	{
		if (curvePathGeometry.Figures.Count == 0 || symbolsGeometryGroup.Children.Count != points.Count) return;

		var curveFigure = curvePathGeometry.Figures[0];

		if (points.Count == 0) return;

		// Helper method to update an existing line segment or add a new one.
		// We assume that index is always ≤ Count.
		static void SetLineSegment(PathFigure figure, int index, Point p)
		{
			if (figure.Segments.Count > index)
			{
				((LineSegment)figure.Segments[index]).Point = p;
			}
			else
			{
				figure.Segments.Add(new LineSegment { Point = p });
			}
		}

		IDataPoint<T, byte> point = points[0];
		T px;
		byte py;
		bool isStepRequired = canSwitchOff && minimumPower > 0;
		int segmentCount = 0;
		double x;
		double y;
		Point p;

		if (draggedPointIndex == 0)
		{
			px = draggedPointInputValue;
			py = draggedPointPower;
		}
		else
		{
			px = point.X;
			py = Math.Min(point.Y, draggedPointPower);
		}

		x = horizontalScale[double.CreateChecked(px)];
		y = verticalScale[py];

		if (double.CreateChecked(px) > horizontalScale.InputMinimum)
		{
			if (isStepRequired)
			{
				curveFigure.StartPoint = new() { X = horizontalScale.OutputMinimum, Y = verticalScale.OutputMinimum };
				p = new() { X = x, Y = verticalScale.OutputMinimum };
				SetLineSegment(curveFigure, segmentCount++, p);
				if (py != 0)
				{
					p.Y = y;
					SetLineSegment(curveFigure, segmentCount++, p);
					isStepRequired = false;
				}
			}
			else
			{
				curveFigure.StartPoint = new() { X = horizontalScale.OutputMinimum, Y = verticalScale[minimumPower] };
				p = new() { X = x, Y = y };
				SetLineSegment(curveFigure, segmentCount++, p);
			}
		}
		else
		{
			p = new() { X = x, Y = y };
			curveFigure.StartPoint = p;
			isStepRequired &= py == 0;
		}

		((EllipseGeometry)symbolsGeometryGroup.Children[0]).Center = p;

		for (int i = 1; i < draggedPointIndex; i++)
		{
			point = points[i];
			px = point.X;
			py = Math.Min(point.Y, draggedPointPower);
			x = horizontalScale[double.CreateChecked(px)];
			y = verticalScale[py];

			p.X = x;
			if (isStepRequired && py != 0)
			{
				SetLineSegment(curveFigure, segmentCount++, p);
				isStepRequired = false;
			}

			p.Y = y;
			SetLineSegment(curveFigure, segmentCount++, p);
			((EllipseGeometry)symbolsGeometryGroup.Children[i]).Center = p;
		}

		if (draggedPointIndex > 0)
		{
			px = draggedPointInputValue;
			py = draggedPointPower;
			x = horizontalScale[double.CreateChecked(px)];
			y = verticalScale[py];

			p.X = x;
			if (isStepRequired && py != 0)
			{
				SetLineSegment(curveFigure, segmentCount++, p);
				isStepRequired = false;
			}

			p.Y = y;
			SetLineSegment(curveFigure, segmentCount++, p);
			((EllipseGeometry)symbolsGeometryGroup.Children[draggedPointIndex]).Center = p;
		}

		for (int i = draggedPointIndex + 1; i < points.Count; i++)
		{
			point = points[i];
			px = point.X;
			py = Math.Max(point.Y, draggedPointPower);
			x = horizontalScale[double.CreateChecked(px)];
			y = verticalScale[py];

			p.X = x;
			if (isStepRequired && py != 0)
			{
				SetLineSegment(curveFigure, segmentCount++, p);
				isStepRequired = false;
			}

			p.Y = y;
			SetLineSegment(curveFigure, segmentCount++, p);
			((EllipseGeometry)symbolsGeometryGroup.Children[i]).Center = p;
		}

		if (double.CreateChecked(px) < horizontalScale.InputMaximum)
		{
			p = new() { X = horizontalScale.OutputMaximum, Y = y };
			SetLineSegment(curveFigure, segmentCount++, p);
		}

		// At the end of the processing, remove any extra line segments that might not be required anymore.
		while (curveFigure.Segments.Count > segmentCount)
		{
			curveFigure.Segments.RemoveAt(curveFigure.Segments.Count - 1);
		}
	}

	private static void UpdateChart
	(
		LinearScale horizontalScale,
		LinearScale verticalScale,
		PathGeometry curvePathGeometry,
		GeometryGroup symbolsGeometryGroup,
		object? points,
		int draggedPointIndex,
		object? draggedPointInputValue,
		byte draggedPointPower,
		byte minimumPower,
		bool canSwitchOff,
		double symbolRadius
	)
	{
		switch (points)
		{
		case IList<IDataPoint<int, byte>> pointsInt32: UpdateChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsInt32, draggedPointIndex, Convert.ToInt32(draggedPointInputValue), draggedPointPower, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<uint, byte>> pointsUInt32: UpdateChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsUInt32, draggedPointIndex, Convert.ToUInt32(draggedPointInputValue), draggedPointPower, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<long, byte>> pointsInt64: UpdateChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsInt64, draggedPointIndex, Convert.ToInt64(draggedPointInputValue), draggedPointPower, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<ulong, byte>> pointsUInt64: UpdateChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsUInt64, draggedPointIndex, Convert.ToUInt64(draggedPointInputValue), draggedPointPower, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<float, byte>> pointsSingle: UpdateChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsSingle, draggedPointIndex, Convert.ToSingle(draggedPointInputValue), draggedPointPower, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<double, byte>> pointsDouble: UpdateChart(horizontalScale, verticalScale, curvePathGeometry, symbolsGeometryGroup, pointsDouble, draggedPointIndex, Convert.ToDouble(draggedPointInputValue), draggedPointPower, minimumPower, canSwitchOff, symbolRadius); break;
		default: return;
		}
	}

	// This is a very simplified algorithm to handle the task based on the following assumptions:
	// - There will never be a large amount of points
	// - Points are always reasonably spaced
	// - Perfect geometric hit test is already performed by the UI framework, but across all points
	// As such, the algorithm
	// - Performs a linear O(N) search instead of a binary search
	// - It does not actually check the radius, which could be a problem if symbols overlap
	// This should be improved if necessary (But be careful, a binary search algorithm must take into account points that overlap so that the result for two matching points is deterministic)
	private static int FindPoint<T>(LinearScale horizontalScale, IList<IDataPoint<T, byte>> points, double x, double symbolRadius)
		where T : INumber<T>
	{
		for (int i = 0; i < points.Count; i++)
		{
			var point = points[i];
			var px = horizontalScale[double.CreateChecked(point.X)];

			if (x <= px + symbolRadius && x >= px - symbolRadius) return i;
		}

		return -1;
	}

	private static int FindPoint(LinearScale horizontalScale, object? points, double x, double symbolRadius)
		=> points switch
		{
			IList<IDataPoint<int, byte>> pointsInt32 => FindPoint(horizontalScale, pointsInt32, x, symbolRadius),
			IList<IDataPoint<uint, byte>> pointsUInt32 => FindPoint(horizontalScale, pointsUInt32, x, symbolRadius),
			IList<IDataPoint<long, byte>> pointsInt64 => FindPoint(horizontalScale, pointsInt64, x, symbolRadius),
			IList<IDataPoint<ulong, byte>> pointsUInt64 => FindPoint(horizontalScale, pointsUInt64, x, symbolRadius),
			IList<IDataPoint<float, byte>> pointsSingle => FindPoint(horizontalScale, pointsSingle, x, symbolRadius),
			IList<IDataPoint<double, byte>> pointsDouble => FindPoint(horizontalScale, pointsDouble, x, symbolRadius),
			_ => -1,
		};

	private static (object, byte) GetPoint<T>(IList<IDataPoint<T, byte>> points, int index)
		where T : INumber<T>
	{
		var point = points[index];
		return (point.X, point.Y);
	}

	private static (object, byte) GetPoint(object? points, int index)
		=> points switch
		{
			IList<IDataPoint<int, byte>> pointsInt32 => GetPoint(pointsInt32, index),
			IList<IDataPoint<uint, byte>> pointsUInt32 => GetPoint(pointsUInt32, index),
			IList<IDataPoint<long, byte>> pointsInt64 => GetPoint(pointsInt64, index),
			IList<IDataPoint<ulong, byte>> pointsUInt64 => GetPoint(pointsUInt64, index),
			IList<IDataPoint<float, byte>> pointsSingle => GetPoint(pointsSingle, index),
			IList<IDataPoint<double, byte>> pointsDouble => GetPoint(pointsDouble, index),
			_ => throw new InvalidOperationException(),
		};

	private static void SetPoint<T>(IList<IDataPoint<T, byte>> points, int index, T x, byte y)
		where T : INumber<T>
	{
		var point = points[index];
		point.X = x;
		point.Y = y;
	}

	private static void SetPoint(object? points, int index, object? x, byte y)
	{
		switch (points)
		{
		case IList<IDataPoint<int, byte>> pointsInt32: SetPoint(pointsInt32, index, Convert.ToInt32(x), y); break;
		case IList<IDataPoint<uint, byte>> pointsUInt32: SetPoint(pointsUInt32, index, Convert.ToUInt32(x), y); break;
		case IList<IDataPoint<long, byte>> pointsInt64: SetPoint(pointsInt64, index, Convert.ToInt64(x), y); break;
		case IList<IDataPoint<ulong, byte>> pointsUInt64: SetPoint(pointsUInt64, index, Convert.ToUInt64(x), y); break;
		case IList<IDataPoint<float, byte>> pointsSingle: SetPoint(pointsSingle, index, Convert.ToSingle(x), y); break;
		case IList<IDataPoint<double, byte>> pointsDouble: SetPoint(pointsDouble, index, Convert.ToDouble(x), y); break;
		default: throw new InvalidOperationException();
		}
	}

	private static void SetAtLeastY<T>(IList<IDataPoint<T, byte>> points, int index, byte y)
		where T : INumber<T>
	{
		var point = points[index];
		point.Y = Math.Max(point.Y, y);
	}

	private static void SetAtLeastY(object? points, int index, byte y)
	{
		switch (points)
		{
		case IList<IDataPoint<int, byte>> pointsInt32: SetAtLeastY(pointsInt32, index, y); break;
		case IList<IDataPoint<uint, byte>> pointsUInt32: SetAtLeastY(pointsUInt32, index, y); break;
		case IList<IDataPoint<long, byte>> pointsInt64: SetAtLeastY(pointsInt64, index, y); break;
		case IList<IDataPoint<ulong, byte>> pointsUInt64: SetAtLeastY(pointsUInt64, index, y); break;
		case IList<IDataPoint<float, byte>> pointsSingle: SetAtLeastY(pointsSingle, index, y); break;
		case IList<IDataPoint<double, byte>> pointsDouble: SetAtLeastY(pointsDouble, index, y); break;
		default: throw new InvalidOperationException();
		}
	}

	private static void SetAtMostY<T>(IList<IDataPoint<T, byte>> points, int index, byte y)
		where T : INumber<T>
	{
		var point = points[index];
		point.Y = Math.Min(point.Y, y);
	}

	private static void SetAtMostY(object? points, int index, byte y)
	{
		switch (points)
		{
		case IList<IDataPoint<int, byte>> pointsInt32: SetAtMostY(pointsInt32, index, y); break;
		case IList<IDataPoint<uint, byte>> pointsUInt32: SetAtMostY(pointsUInt32, index, y); break;
		case IList<IDataPoint<long, byte>> pointsInt64: SetAtMostY(pointsInt64, index, y); break;
		case IList<IDataPoint<ulong, byte>> pointsUInt64: SetAtMostY(pointsUInt64, index, y); break;
		case IList<IDataPoint<float, byte>> pointsSingle: SetAtMostY(pointsSingle, index, y); break;
		case IList<IDataPoint<double, byte>> pointsDouble: SetAtMostY(pointsDouble, index, y); break;
		default: throw new InvalidOperationException();
		}
	}

	private void OnSymbolsPathPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (_capturedPointer is not null || _symbolsPath is null || _layoutGrid is null || _horizontalScale is null || _verticalScale is null) return;

		var point = e.GetCurrentPoint(_layoutGrid);
		if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;

		var points = Points;
		if (FindPoint(_horizontalScale, points, point.Position.X, SymbolRadius) is int index and >= 0 && ((UIElement)sender).CapturePointer(e.Pointer))
		{
			_capturedPointer = e.Pointer;
			_draggedPointIndex = index;
			(_draggedPointCurrentInputValue, _draggedPointCurrentPower) = GetPoint(points, index);
			_draggedPointRelativePower = _verticalScale.Inverse(point.Position.Y) - _draggedPointCurrentPower;
			if (_powerValueToolTip is not null)
			{
				_powerValueToolTip.Content = $"{_draggedPointCurrentPower} %";
				_powerValueToolTip.IsOpen = true;
			}
		}
	}

	private void OnSymbolsPathPointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (_capturedPointer is not null && _capturedPointer.PointerId != e.Pointer.PointerId || _horizontalScale is null) return;

		var point = e.GetCurrentPoint(_layoutGrid);

		var points = Points;
		if (_capturedPointer is null)
		{
			if (_powerValueToolTip is not null && FindPoint(_horizontalScale, points, point.Position.X, SymbolRadius) is int index and >= 0)
			{
				var (_, y) = GetPoint(Points, index);
				_powerValueToolTip.Content = $"{y} %";
				// Disabling then enabling the tooltip is the trick that is used by ColorSpectrum to move the tooltip to the current mouse position.
				_powerValueToolTip.IsEnabled = false;
				_powerValueToolTip.IsEnabled = true;
			}
			return;
		}

		if (!point.Properties.IsLeftButtonPressed || _verticalScale is null) return;

		byte minimumPower = MinimumPower;
		byte power = (byte)Math.Clamp(_verticalScale.Inverse(point.Position.Y) - _draggedPointRelativePower, 0, 100);

		if (power < minimumPower)
		{
			power = CanSwitchOff ?
				power > minimumPower / 2 ?
					minimumPower :
					(byte)0 :
				minimumPower;
		}

		if (power != _draggedPointCurrentPower)
		{
			_draggedPointCurrentPower = power;
			RefreshCurve();
			if (_powerValueToolTip is not null)
			{
				_powerValueToolTip.Content = $"{power} %";
				// Disabling then enabling the tooltip is the trick that is used by ColorSpectrum to move the tooltip to the current mouse position.
				_powerValueToolTip.IsEnabled = false;
				_powerValueToolTip.IsEnabled = true;
			}
		}
	}

	private void OnSymbolsPathPointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (_capturedPointer is null || _capturedPointer.PointerId != e.Pointer.PointerId) return;

		if (_symbolsPath is not null && _layoutGrid is not null && _horizontalScale is not null && _verticalScale is not null)
		{
			var point = e.GetCurrentPoint(_layoutGrid);
			if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
			{
				int pointCount = ((System.Collections.ICollection)Points!).Count;

				byte minimumPower = MinimumPower;
				byte power = (byte)Math.Clamp(_verticalScale.Inverse(point.Position.Y) - _draggedPointRelativePower, 0, 100);

				if (power < minimumPower)
				{
					power = CanSwitchOff ?
						power > minimumPower / 2 ?
							minimumPower :
							(byte)0 :
						minimumPower;
				}

				for (int i = 0; i < _draggedPointIndex; i++)
				{
					SetAtMostY(Points, i, power);
				}

				_draggedPointCurrentPower = power;
				SetPoint(Points, _draggedPointIndex, _draggedPointCurrentInputValue, _draggedPointCurrentPower);

				for (int i = _draggedPointIndex + 1; i < pointCount; i++)
				{
					SetAtLeastY(Points, i, power);
				}
			}
		}

		if (_powerValueToolTip is not null)
		{
			_powerValueToolTip.IsOpen = false;
		}

		_capturedPointer = null;
		_draggedPointIndex = -1;

		RefreshCurve();
	}

	private void OnSymbolsPathPointerCanceled(object sender, PointerRoutedEventArgs e)
	{
		if (_capturedPointer is null || _capturedPointer.PointerId != e.Pointer.PointerId) return;

		if (_layoutGrid is null || _horizontalScale is null || _verticalScale is null)
		{
			if (_powerValueToolTip is not null)
			{
				_powerValueToolTip.IsOpen = false;
			}

			_capturedPointer = null;
			_draggedPointIndex = -1;
			RefreshCurve();
			return;
		}
	}

	private void OnSymbolsPathPointerCaptureLost(object sender, PointerRoutedEventArgs e)
	{
		if (_capturedPointer is null || _capturedPointer.PointerId != e.Pointer.PointerId) return;

		if (_layoutGrid is null || _horizontalScale is null || _verticalScale is null)
		{
			if (_powerValueToolTip is not null)
			{
				_powerValueToolTip.IsOpen = false;
			}

			_capturedPointer = null;
			_draggedPointIndex = -1;
			RefreshCurve();
			return;
		}
	}
}

