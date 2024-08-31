using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Exo.Settings.Ui.Controls;

[TemplatePart(Name = LayoutGridPartName, Type = typeof(Grid))]
[TemplatePart(Name = BackgroundCirclePathPartName, Type = typeof(Path))]
[TemplatePart(Name = ForegroundArcPathPartName, Type = typeof(Path))]
internal partial class Gauge : Control
{
	private const string LayoutGridPartName = "PART_LayoutGrid";
	private const string BackgroundCirclePathPartName = "PART_BackgroundCirclePath";
	private const string ForegroundArcPathPartName = "PART_ForegroundArcPath";

	private Path? _backgroundCirclePath;
	private Path? _foregroundArcPath;
	private Grid? _layoutGrid;

	// NB: For the foreground, we NEED to use either an arc or an ellipse depending on whether we reached 100%
	// The geometry objects cannot be shared, so we need one for the background and one for the foreground.
	private readonly EllipseGeometry _backgroundCircleGeometry;
	private readonly EllipseGeometry _foregroundCircleGeometry;
	private readonly PathGeometry _foregroundArcGeometry;
	private double _strokeThickness;

	public Gauge()
	{
		_backgroundCircleGeometry = new();
		_foregroundCircleGeometry = new();
		_foregroundArcGeometry = new();
		SizeChanged += static (s, e) => ((Gauge)s).OnSizeChanged(e);
	}

	protected override void OnApplyTemplate()
	{
		DetachParts();
		_layoutGrid = GetTemplateChild(LayoutGridPartName) as Grid;
		_backgroundCirclePath = GetTemplateChild(BackgroundCirclePathPartName) as Path;
		_foregroundArcPath = GetTemplateChild(ForegroundArcPathPartName) as Path;
		AttachParts();
	}

	private void DetachParts()
	{
		ResetPath(_backgroundCirclePath);
		ResetPath(_foregroundArcPath);
	}

	private void AttachParts()
	{
		InitializePath(_backgroundCirclePath, _backgroundCircleGeometry, _strokeThickness);
		InitializePath(_foregroundArcPath, 1 - GetPercentage() > double.Epsilon ? _foregroundArcGeometry : _foregroundCircleGeometry, _strokeThickness);
		RefreshGeometry(true);
	}

	private static void ResetPath(Path? path)
	{
		if (path is null) return;
		path.ClearValue(Path.DataProperty);
		path.ClearValue(Shape.StrokeThicknessProperty);
	}

	private static void InitializePath(Path? path, Geometry? data, double thickness)
	{
		if (path is null) return;
		path.Data = data;
		path.StrokeThickness = thickness;
	}

	private void OnSizeChanged(SizeChangedEventArgs e) => RefreshGeometry(true);

	private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as Gauge)?.OnPropertyChanged(e);

	private void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.Property == ValueProperty || e.Property == MaximumProperty)
		{
			RefreshGeometry(false);
		}
		else if (e.Property == CircleThicknessPercentageProperty)
		{
			RefreshGeometry(true);
		}
	}

	private double GetPercentage()
	{
		double value = Value;
		double maximum = Maximum;

		return double.IsRealNumber(value) && value >= 0 && double.IsRealNumber(maximum) && maximum >= 0 ? Math.Clamp(value / maximum, 0, 1) : 0;
	}

	private void RefreshGeometry(bool isResize)
	{
		if (_layoutGrid is not { } layoutGrid) return;

		double width = layoutGrid.ActualWidth;
		double height = layoutGrid.ActualHeight;
		double size = Math.Min(width, height);

		if (size <= 0) return;

		int thicknessPercentage = Math.Clamp(CircleThicknessPercentage, 0, 100);
		double thicknessRatio = thicknessPercentage / 100d;

		double thickness = thicknessRatio * size;

		double radius = 0.5 * (size - thickness);
		double cx = 0.5 * width;
		double cy = 0.5 * height;

		if (isResize)
		{
			_strokeThickness = thickness;
			RefreshCircle(_backgroundCircleGeometry, cx, cy, radius);
			RefreshCircle(_foregroundCircleGeometry, cx, cy, radius);
			if (_backgroundCirclePath is { } circlePath) circlePath.StrokeThickness = _strokeThickness;
			if (_foregroundArcPath is { } arcPath) arcPath.StrokeThickness = _strokeThickness;
		}

		double percentage = GetPercentage();

		if (1 - percentage > double.Epsilon)
		{
			RefreshArc(_foregroundArcGeometry, isResize, cx, cy, radius, percentage);
			if (_foregroundArcPath is not null && _foregroundArcPath.Data != _foregroundArcGeometry) _foregroundArcPath.Data = _foregroundArcGeometry;
		}
		else
		{
			if (_foregroundArcPath is not null && _foregroundArcPath.Data != _foregroundCircleGeometry)
			{
				if (!isResize) RefreshCircle(_foregroundCircleGeometry, cx, cy, radius);
				_foregroundArcPath.Data = _foregroundCircleGeometry;
			}
		}
	}

	private static void RefreshCircle(EllipseGeometry geometry, double cx, double cy, double radius)
	{
		geometry.Center = new() { X = cx, Y = cy };
		geometry.RadiusX = radius;
		geometry.RadiusY = radius;
	}

	private static void RefreshArc(PathGeometry geometry, bool isResize, double cx, double cy, double radius, double percentage)
	{
		PathFigure arcFigure;
		if (geometry.Figures.Count == 0)
		{
			geometry.Figures.Add(arcFigure = new() { IsFilled = false, StartPoint = new() { X = cx, Y = cy - radius } });
		}
		else
		{
			arcFigure = geometry.Figures[0];
			if (isResize) arcFigure.StartPoint = new() { X = cx, Y = cy - radius };
		}

		if (percentage > 0)
		{
			ArcSegment arc;
			if (arcFigure.Segments.Count == 0)
			{
				arcFigure.Segments.Add(arc = new() { SweepDirection = SweepDirection.Clockwise, Size = new() { Width = radius, Height = radius } });
			}
			else
			{
				arc = (ArcSegment)arcFigure.Segments[0];
				if (isResize) arc.Size = new() { Width = radius, Height = radius };
			}

			double θ = 2 * Math.PI * percentage;

			arc.Point = new() { X = cx + radius * Math.Sin(θ), Y = cy - radius * Math.Cos(θ) };
			arc.IsLargeArc = percentage > 0.5;
		}
		else
		{
			arcFigure.Segments.Clear();
			arcFigure.IsClosed = false;
		}
	}
}
