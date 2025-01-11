using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Exo.Settings.Ui.Controls;

internal sealed class TickLayout : NonVirtualizingLayout
{
	public Orientation Orientation
	{
		get => (Orientation)GetValue(OrientationProperty);
		set => SetValue(OrientationProperty, value);
	}

	public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register
	(
		nameof(Orientation),
		typeof(Orientation),
		typeof(TickLayout),
		new PropertyMetadata(Orientation.Vertical, static (s, e) => ((TickLayout)s).OnOrientationChanged())
	);

	public double TickHalfSize
	{
		get => (double)GetValue(TickHalfSizeProperty);
		set => SetValue(TickHalfSizeProperty, value);
	}

	public static readonly DependencyProperty TickHalfSizeProperty = DependencyProperty.Register
	(
		nameof(TickHalfSize),
		typeof(double),
		typeof(TickLayout),
		new PropertyMetadata(10d)
	);

	public TickLayout()
	{
	}

	private void OnOrientationChanged() => InvalidateMeasure();

	protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
	{
		if (context.Children.Count == 0) return new(0, 0);

		var tickSize = (float)(2 * TickHalfSize);

		// We chose to allow overlap of children in case there would not be enough space. This is the simplest way to allocate space.
		Size availableSizePerChild = Orientation == Orientation.Vertical ?
			new(availableSize._width, Math.Min(availableSize._height, tickSize)) :
			new(Math.Min(availableSize._width, tickSize), availableSize._height);

		// Query all children for their size with one of the two dimensions already fixed.
		float maxIdealWidth = 0;
		float maxIdealHeight = 0;
		foreach (var child in context.Children)
		{
			child.Measure(availableSizePerChild);
			var size = child.DesiredSize;
			if (float.IsFinite(size._width) && size._width > maxIdealWidth) maxIdealWidth = size._width;
			if (float.IsFinite(size._height) && size._height > maxIdealHeight) maxIdealHeight = size._height;
		}

		// Compute the final size using the variable dimension as we are allowed to.
		if (Orientation == Orientation.Vertical)
		{
			return new(Math.Min(availableSize._width, maxIdealWidth), Math.Min(availableSize._height, availableSizePerChild._height * context.Children.Count));
		}
		else
		{
			return new(Math.Min(availableSize._width, availableSizePerChild._width * context.Children.Count), Math.Min(availableSize._height, maxIdealWidth));
		}
	}

	protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
	{
		var children = context.Children;
		if (children.Count == 0) return new Size(0, 0);
		float width = float.IsFinite(finalSize._width) ? finalSize._width : 0;
		float height = float.IsFinite(finalSize._height) ? finalSize._height : 0;
		float size = (float)(2 * TickHalfSize);

		if (Orientation == Orientation.Vertical)
		{
			float space = height - size;
			if (space < 0)
			{
				for (int i = 0; i < children.Count; i++)
				{
					children[i].Arrange(new Rect(0, 0, width, height));
				}
			}
			else
			{
				float h = space / Math.Max(1, children.Count - 1);
				for (int i = 0; i < children.Count; i++)
				{
					children[i].Arrange(new Rect(0, i * h, width, size));
				}
			}
		}
		else
		{
			float space = width - size;
			if (space < 0)
			{
				for (int i = 0; i < children.Count; i++)
				{
					children[i].Arrange(new Rect(0, 0, width, height));
				}
			}
			else
			{
				float w = space / Math.Max(1, children.Count - 1);
				for (int i = 0; i < children.Count; i++)
				{
					children[i].Arrange(new Rect(i * w, 0, size, height));
				}
			}
		}

		return new(width, height);
	}
}
