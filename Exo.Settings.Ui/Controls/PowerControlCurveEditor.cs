using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
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
	private Path? _symbolsPath;
	private Path? _horizontalGridLinesPath;
	private Path? _verticalGridLinesPath;
	private ItemsRepeater? _horizontalTickItemsRepeater;
	private ItemsRepeater? _verticalTickItemsRepeater;
	private Grid? _layoutGrid;
	private readonly PathGeometry _verticalGridLinesPathGeometry;
	private readonly PathGeometry _horizontalGridLinesPathGeometry;
	private readonly PathGeometry _curvePathGeometry;
	private readonly GeometryGroup _symbolsGeometryGroup;
	private readonly NotifyCollectionChangedEventHandler _pointsCollectionChanged;
	private LinearScale? _horizontalScale;
	private LinearScale? _verticalScale;
	private double[] _horizontalTicks;
	private readonly double[] _verticalTicks;

	public PowerControlCurveEditor()
	{
		_pointsCollectionChanged = OnPointsCollectionChanged;
		_horizontalTicks = [];
		_verticalTicks = [100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 0];

		_verticalGridLinesPathGeometry = new();
		_horizontalGridLinesPathGeometry = new();
		_curvePathGeometry = new();
		_symbolsGeometryGroup = new();

		Loaded += static (s, e) => ((PowerControlCurveEditor)s).OnLoaded(e);
		Unloaded += static (s, e) => ((PowerControlCurveEditor)s).OnUnloaded(e);
		SizeChanged += static (s, e) => ((PowerControlCurveEditor)s).OnSizeChanged(e);
	}

	private void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.Property == PointsProperty) OnPointsChanged(e);
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
		AttachParts();
		RefreshScales(true);
		RefreshCurve();
	}

	private void DetachParts()
	{
		_horizontalScale = null;
		_verticalScale = null;
		_horizontalTicks = [];
		SetData(_curvePath, null);
		SetData(_symbolsPath, null);
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
		if (_verticalTickItemsRepeater is not null) _verticalTickItemsRepeater.ItemsSource = _verticalTicks;
		if (_horizontalTickItemsRepeater is not null) _horizontalTickItemsRepeater.ItemsSource = _horizontalTicks;
	}

	private void RefreshScales(bool isResize)
	{
		double width = _layoutGrid?.ActualWidth ?? 1;
		double height = _layoutGrid?.ActualHeight ?? 1;
		double minX = GetOrDefault(MinimumInputValue, double.PositiveInfinity);
		double maxX = GetOrDefault(MaximumInputValue, double.NegativeInfinity);
		LinearScale horizontalScale;
		LinearScale verticalScale;
		double[] horizontalTicks;

		switch (Points)
		{
		case IList<IDataPoint<int, byte>> pointsInt32: (horizontalScale, verticalScale, horizontalTicks) = GenerateScales(pointsInt32, minX, maxX, width, height); break;
		case IList<IDataPoint<uint, byte>> pointsUInt32: (horizontalScale, verticalScale, horizontalTicks) = GenerateScales(pointsUInt32, minX, maxX, width, height); break;
		case IList<IDataPoint<long, byte>> pointsInt64: (horizontalScale, verticalScale, horizontalTicks) = GenerateScales(pointsInt64, minX, maxX, width, height); break;
		case IList<IDataPoint<ulong, byte>> pointsUInt64: (horizontalScale, verticalScale, horizontalTicks) = GenerateScales(pointsUInt64, minX, maxX, width, height); break;
		case IList<IDataPoint<float, byte>> pointsSingle: (horizontalScale, verticalScale, horizontalTicks) = GenerateScales(pointsSingle, minX, maxX, width, height); break;
		case IList<IDataPoint<double, byte>> pointsDouble: (horizontalScale, verticalScale, horizontalTicks) = GenerateScales(pointsDouble, minX, maxX, width, height); break;
		default: (horizontalScale, verticalScale, horizontalTicks) = GenerateScales(minX, maxX, width, height); break;
		}

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

		byte minimumPower = (byte)MinimumPower;
		bool canSwitchOff = CanSwitchOff;
		double symbolRadius = SymbolRadius;

		switch (Points)
		{
		case IList<IDataPoint<int, byte>> pointsInt32: RedrawChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, pointsInt32, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<uint, byte>> pointsUInt32: RedrawChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, pointsUInt32, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<long, byte>> pointsInt64: RedrawChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, pointsInt64, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<ulong, byte>> pointsUInt64: RedrawChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, pointsUInt64, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<float, byte>> pointsSingle: RedrawChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, pointsSingle, minimumPower, canSwitchOff, symbolRadius); break;
		case IList<IDataPoint<double, byte>> pointsDouble: RedrawChart(_horizontalScale, _verticalScale, _curvePathGeometry, _symbolsGeometryGroup, pointsDouble, minimumPower, canSwitchOff, symbolRadius); break;
		default: return;
		}
	}

	private static void SetData(Path? path, Geometry? data)
	{
		if (path is null) return;
		path.Data = data;
	}

	private static double GetOrDefault(object? value, double defaultValue)
		=> value is not null ? Convert.ToDouble(value) : default;

	private static (LinearScale HorizontalScale, LinearScale VerticalScale, double[] HorizontalTicks) GenerateScales<T>(IList<IDataPoint<T, byte>> points, double minX, double maxX, double outputWidth, double outputHeight)
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

		return GenerateScales(minX, maxX, outputWidth, outputHeight);
	}

	private static (LinearScale HorizontalScale, LinearScale VerticalScale, double[] HorizontalTicks) GenerateScales(double minX, double maxX, double outputWidth, double outputHeight)
	{
		// Anchor the scale to zero if necessary.
		if (maxX < 0) maxX = 0;
		if (minX > 0) minX = 0;

		// Force the chart to not be fully empty if the min and max are both zero. (result of previous adjustments)
		if (minX == maxX) maxX = 1;

		var (scaleMinX, scaleMaxX, tickSpacingX) = NiceScale.Compute(minX, maxX);

		var horizontalScale = new LinearScale(scaleMinX, scaleMaxX, 0.5, outputWidth - 0.5);
		var verticalScale = new LinearScale(0, 100, outputHeight - 0.5, 0.5);

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
		if (double.CreateChecked(point.X) > horizontalScale.InputMinimum)
		{
			double startY = verticalScale[canSwitchOff ? (byte)0 : minimumPower];
			curveFigure.StartPoint = new() { X = horizontalScale.OutputMinimum, Y = startY };
			if (canSwitchOff)
			{
				curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = startY } });
				if (point.Y != 0)
				{
					curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = y } });
				}
			}
			else
			{
				curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = y } });
			}
		}
		else
		{
			curveFigure.StartPoint = new() { X = x, Y = y };
		}

		symbolsGeometryGroup.Children.Add(new EllipseGeometry() { Center = new() { X = x, Y = y }, RadiusX = symbolRadius, RadiusY = symbolRadius });

		for (int i = 1; i < points.Count; i++)
		{
			point = points[i];
			x = horizontalScale[double.CreateChecked(point.X)];
			y = verticalScale[point.Y];
			curveFigure.Segments.Add(new LineSegment() { Point = new() { X = x, Y = y } });
			symbolsGeometryGroup.Children.Add(new EllipseGeometry() { Center = new() { X = x, Y = y }, RadiusX = symbolRadius, RadiusY = symbolRadius });
		}

		if (double.CreateChecked(point.X) < horizontalScale.InputMaximum)
		{
			curveFigure.Segments.Add(new LineSegment() { Point = new() { X = horizontalScale.OutputMaximum, Y = y } });
		}
	}
}

internal sealed class LinearScale
{
	private readonly double _inputMinimum;
	private readonly double _inputAmplitude;

	private readonly double _outputMinimum;
	private readonly double _outputAmplitude;

	private readonly double _inputMaximum;
	private readonly double _outputMaximum;

	public double InputMinimum => _inputMinimum;
	public double InputMaximum => _inputMaximum;

	public double OutputMinimum => _outputMinimum;
	public double OutputMaximum => _outputMaximum;

	public double InputAmplitude => _inputAmplitude;
	public double OutputAmplitude => _outputAmplitude;

	public LinearScale(double inputMinimum, double inputMaximum, double outputMinimum, double outputMaximum)
	{
		_inputMinimum = inputMinimum;
		_inputAmplitude = inputMaximum - inputMinimum;
		_outputMinimum = outputMinimum;
		_outputAmplitude = outputMaximum - outputMinimum;
		_inputMaximum = inputMaximum;
		_outputMaximum = outputMaximum;
	}

	public double this[double value] => _outputMinimum + (value - _inputMinimum) * _outputAmplitude / _inputAmplitude;

	public double Inverse(double value) => _inputMinimum + (value - _outputMinimum) * _inputAmplitude / _outputAmplitude;
}

