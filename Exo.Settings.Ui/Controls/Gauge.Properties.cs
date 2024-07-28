using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui.Controls;

internal partial class Gauge
{
	public double Value
	{
		get => (double)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(Gauge), new(0d, OnPropertyChanged));

	public double Maximum
	{
		get => (double)GetValue(MaximumProperty);
		set => SetValue(MaximumProperty, value);
	}

	public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(Gauge), new(100d, OnPropertyChanged));

	public int CircleThicknessPercentage
	{
		get => (int)GetValue(CircleThicknessPercentageProperty);
		set => SetValue(CircleThicknessPercentageProperty, value);
	}

	public static readonly DependencyProperty CircleThicknessPercentageProperty = DependencyProperty.Register(nameof(CircleThicknessPercentage), typeof(int), typeof(Gauge), new(10, OnPropertyChanged));

	public PenLineCap ArcLineCap
	{
		get => (PenLineCap)GetValue(ArcLineCapProperty);
		set => SetValue(ArcLineCapProperty, value);
	}

	public static readonly DependencyProperty ArcLineCapProperty = DependencyProperty.Register(nameof(ArcLineCap), typeof(PenLineCap), typeof(Gauge), new PropertyMetadata(PenLineCap.Flat));

	public Brush BackgroundCircleStroke
	{
		get => (Brush)GetValue(BackgroundCircleStrokeProperty);
		set => SetValue(BackgroundCircleStrokeProperty, value);
	}

	public static readonly DependencyProperty BackgroundCircleStrokeProperty = DependencyProperty.Register(nameof(BackgroundCircleStroke), typeof(Brush), typeof(Gauge), new PropertyMetadata(new SolidColorBrush(Colors.Gray)));

	public Brush ForegroundArcStroke
	{
		get => (Brush)GetValue(ForegroundArcStrokeProperty);
		set => SetValue(ForegroundArcStrokeProperty, value);
	}

	public static readonly DependencyProperty ForegroundArcStrokeProperty = DependencyProperty.Register(nameof(ForegroundArcStroke), typeof(Brush), typeof(Gauge), new PropertyMetadata(new SolidColorBrush(Colors.Lime)));
}
