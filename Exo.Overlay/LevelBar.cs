using System.Windows;
using System.Windows.Media;

namespace Exo.Overlay;

public class LevelBar : FrameworkElement
{
	private sealed class PropertyChangedHandler
	{
		public static readonly PropertyChangedCallback Instance = new(new PropertyChangedHandler().OnPropertyChanged);

		private PropertyChangedHandler() { }

		private void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LevelBar)d).OnRenderingPropertyChanged(e);
	}

	public int Value
	{
		get => (int)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(int), typeof(LevelBar), new PropertyMetadata(0, PropertyChangedHandler.Instance));

	public int Maximum
	{
		get => (int)GetValue(MaximumProperty);
		set => SetValue(MaximumProperty, value);
	}

	public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(LevelBar), new PropertyMetadata(0, PropertyChangedHandler.Instance));

	public Brush ActiveFill
	{
		get => (Brush)GetValue(ActiveFillProperty);
		set => SetValue(ActiveFillProperty, value);
	}

	public static readonly DependencyProperty ActiveFillProperty = DependencyProperty.Register(nameof(ActiveFill), typeof(Brush), typeof(LevelBar), new PropertyMetadata(Brushes.White, PropertyChangedHandler.Instance));

	public Brush InactiveFill
	{
		get => (Brush)GetValue(InactiveFillProperty);
		set => SetValue(InactiveFillProperty, value);
	}

	public static readonly DependencyProperty InactiveFillProperty = DependencyProperty.Register(nameof(InactiveFill), typeof(Brush), typeof(LevelBar), new PropertyMetadata(Brushes.Gray, PropertyChangedHandler.Instance));

	public double BlockSpacing
	{
		get => (double)GetValue(BlockSpacingProperty);
		set => SetValue(BlockSpacingProperty, value);
	}

	public static readonly DependencyProperty BlockSpacingProperty = DependencyProperty.Register(nameof(BlockSpacing), typeof(double), typeof(LevelBar), new PropertyMetadata(2d, PropertyChangedHandler.Instance));

	public double MinimumBlockSize
	{
		get => (double)GetValue(MinimumBlockSizeProperty);
		set => SetValue(MinimumBlockSizeProperty, value);
	}

	public static readonly DependencyProperty MinimumBlockSizeProperty = DependencyProperty.Register(nameof(MinimumBlockSize), typeof(double), typeof(LevelBar), new PropertyMetadata(5d, PropertyChangedHandler.Instance));

	private void OnRenderingPropertyChanged(DependencyPropertyChangedEventArgs e) => InvalidateVisual();

	protected override int VisualChildrenCount => 0;

	protected override Visual GetVisualChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));

	protected override void OnRender(DrawingContext ctx)
	{
		double width = ActualWidth;
		double height = ActualHeight;

		if (double.IsNaN(width) || double.IsNaN(height)) return;

		double minBlockSize = MinimumBlockSize;
		double spacing = BlockSpacing;

		int max = Maximum;
		int level = Value;

		var activeFill = ActiveFill;
		var inactiveFill = InactiveFill;

		if (max == 0 || width == 0 || height == 0) return;

		double augmentedWidth = width + spacing;

		int maxBlockCount = (int)(augmentedWidth / (minBlockSize + spacing));

		if (max > maxBlockCount)
		{
			ctx.DrawRectangle(inactiveFill, null, new(0, 0, width, height));
			ctx.DrawRectangle(activeFill, null, new(0, 0, level * width / max, height));
		}
		else
		{
			int blockWidth = (int)(augmentedWidth / max - spacing);
			double step = blockWidth + spacing;
			double offset = (augmentedWidth - step * max) / 2;
			for (int i = 0; i < max; i++)
			{
				ctx.DrawRectangle(i < level ? activeFill : inactiveFill, null, new(offset, 0, blockWidth, height));
				offset += step;
			}
		}
	}
}
