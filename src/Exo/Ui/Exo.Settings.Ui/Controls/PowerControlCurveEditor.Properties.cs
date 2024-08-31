using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui.Controls;

internal partial class PowerControlCurveEditor
{
	private static DependencyProperty RegisterProperty<T>(string name, T defaultValue)
		=> DependencyProperty.Register(name, typeof(T), typeof(PowerControlCurveEditor), new PropertyMetadata(defaultValue));

	private static DependencyProperty RegisterPropertyWithChangeHandler<T>(string name, T defaultValue)
		=> DependencyProperty.Register(name, typeof(T), typeof(PowerControlCurveEditor), new PropertyMetadata(defaultValue, OnPropertyChanged));

	public int? SelectedIndex
	{
		get => (int?)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	public static readonly DependencyProperty SelectedIndexProperty = RegisterPropertyWithChangeHandler<int?>(nameof(SelectedIndex), null);

	public string? Unit
	{
		get => (string)GetValue(UnitProperty);
		set => SetValue(UnitProperty, value);
	}

	public static readonly DependencyProperty UnitProperty = RegisterPropertyWithChangeHandler<string?>(nameof(Unit), null);

	public byte MinimumPower
	{
		get => (byte)GetValue(MinimumPowerProperty);
		set => SetValue(MinimumPowerProperty, value);
	}

	// TODO: Should be byte and not int, but obviously WinUI is broken. We can store a byte inside of an int property, but a byte into a byte property wouldn't work.
	public static readonly DependencyProperty MinimumPowerProperty = RegisterPropertyWithChangeHandler(nameof(MinimumPower), 0);

	public bool CanSwitchOff
	{
		get => (bool)GetValue(CanSwitchOffProperty);
		set => SetValue(CanSwitchOffProperty, value);
	}

	public static readonly DependencyProperty CanSwitchOffProperty = RegisterPropertyWithChangeHandler(nameof(CanSwitchOff), true);

	public bool IsFloatingPoint
	{
		get => (bool)GetValue(IsFloatingPointProperty);
		set => SetValue(IsFloatingPointProperty, value);
	}

	public static readonly DependencyProperty IsFloatingPointProperty = RegisterPropertyWithChangeHandler(nameof(IsFloatingPoint), false);

	public object? MinimumInputValue
	{
		get => GetValue(MinimumInputValueProperty);
		set => SetValue(MinimumInputValueProperty, value);
	}

	public static readonly DependencyProperty MinimumInputValueProperty = RegisterPropertyWithChangeHandler<object?>(nameof(MinimumInputValue), null);

	public object? MaximumInputValue
	{
		get => GetValue(MaximumInputValueProperty);
		set => SetValue(MaximumInputValueProperty, value);
	}

	public static readonly DependencyProperty MaximumInputValueProperty = RegisterPropertyWithChangeHandler<object?>(nameof(MaximumInputValue), null);

	public object? Points
	{
		get => GetValue(PointsProperty);
		set => SetValue(PointsProperty, value);
	}

	public static readonly DependencyProperty PointsProperty = RegisterPropertyWithChangeHandler<object?>(nameof(Points), null);

	public Brush CurveStroke
	{
		get => (Brush)GetValue(CurveStrokeProperty);
		set => SetValue(CurveStrokeProperty, value);
	}

	public static readonly DependencyProperty CurveStrokeProperty = RegisterProperty<Brush>(nameof(CurveStroke), new SolidColorBrush());

	public double CurveStrokeThickness
	{
		get => (double)GetValue(CurveStrokeThicknessProperty);
		set => SetValue(CurveStrokeThicknessProperty, value);
	}

	public static readonly DependencyProperty CurveStrokeThicknessProperty = RegisterProperty(nameof(CurveStrokeThickness), 1d);

	public PenLineJoin CurveStrokeLineJoin
	{
		get => (PenLineJoin)GetValue(CurveStrokeLineJoinProperty);
		set => SetValue(CurveStrokeLineJoinProperty, value);
	}

	public static readonly DependencyProperty CurveStrokeLineJoinProperty = RegisterProperty(nameof(CurveStrokeLineJoin), PenLineJoin.Round);

	public Brush SymbolFill
	{
		get => (Brush)GetValue(SymbolFillProperty);
		set => SetValue(SymbolFillProperty, value);
	}

	public static readonly DependencyProperty SymbolFillProperty = RegisterProperty<Brush>(nameof(SymbolFill), new SolidColorBrush());

	public Brush SymbolStroke
	{
		get => (Brush)GetValue(SymbolStrokeProperty);
		set => SetValue(SymbolStrokeProperty, value);
	}

	public static readonly DependencyProperty SymbolStrokeProperty = RegisterProperty<Brush>(nameof(SymbolStroke), new SolidColorBrush());

	public double SymbolStrokeThickness
	{
		get => (double)GetValue(SymbolStrokeThicknessProperty);
		set => SetValue(SymbolStrokeThicknessProperty, value);
	}

	public static readonly DependencyProperty SymbolStrokeThicknessProperty = RegisterProperty(nameof(SymbolStrokeThickness), 1d);

	public double SymbolRadius
	{
		get => (double)GetValue(SymbolRadiusProperty);
		set => SetValue(SymbolRadiusProperty, value);
	}

	public static readonly DependencyProperty SymbolRadiusProperty = RegisterPropertyWithChangeHandler(nameof(SymbolRadius), 2d);

	public Brush HorizontalGridStroke
	{
		get => (Brush)GetValue(HorizontalGridStrokeProperty);
		set => SetValue(HorizontalGridStrokeProperty, value);
	}

	public static readonly DependencyProperty HorizontalGridStrokeProperty = RegisterProperty<Brush>(nameof(HorizontalGridStroke), new SolidColorBrush());

	public Brush VerticalGridStroke
	{
		get => (Brush)GetValue(VerticalGridStrokeProperty);
		set => SetValue(VerticalGridStrokeProperty, value);
	}

	public static readonly DependencyProperty VerticalGridStrokeProperty = RegisterProperty<Brush>(nameof(VerticalGridStroke), new SolidColorBrush());
}
