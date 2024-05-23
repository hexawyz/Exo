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

	public TickLayout()
	{
	}

	private void OnOrientationChanged() => InvalidateMeasure();

	protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
	{
		if (context.Children.Count == 0) return new(0, 0);

		// In the first pass, all children are queried for their ideal size as if they were alone.
		float maxIdealWidth = 0;
		float maxIdealHeight = 0;
		foreach (var child in context.Children)
		{
			child.Measure(availableSize);
			var size = child.DesiredSize;
			if (float.IsFinite(size._width) && size._width > maxIdealWidth) maxIdealWidth = size._width;
			if (float.IsFinite(size._height) && size._height > maxIdealHeight) maxIdealHeight = size._height;
		}

		// After the initial measures, we can determine the biggest ideal size, capped by availableSize.
		float maxTotalWidth = maxIdealWidth;
		float maxTotalHeight = maxIdealHeight;
		float itemWidth = maxTotalWidth;
		float itemHeight = maxTotalHeight;
		if (Orientation == Orientation.Vertical)
		{
			if (maxTotalWidth > availableSize._width) maxTotalWidth = availableSize._width;
			maxTotalHeight *= context.Children.Count;
			if (maxTotalHeight > availableSize._height) maxTotalHeight = availableSize._height;

			itemHeight = maxTotalHeight / context.Children.Count;
		}
		else
		{
			if (maxTotalHeight > availableSize._height) maxTotalHeight = availableSize._height;
			maxTotalWidth *= context.Children.Count;
			if (maxTotalWidth > availableSize._width) maxTotalWidth = availableSize._width;

			itemWidth = maxTotalHeight / context.Children.Count;
		}

		// The second pass will determine if we can shrink the size we determined above.
		// NB: We could loop and adjust many times, but we would have no guarantee that the algorithm would converge at some point.
		var itemSize = new Size(itemWidth, itemHeight);
		float maxIdealWidth2 = 0;
		float maxIdealHeight2 = 0;
		foreach (var child in context.Children)
		{
			child.Measure(itemSize);
			var size = child.DesiredSize;
			if (float.IsFinite(size._width) && size._width > maxIdealWidth2) maxIdealWidth2 = size._width;
			if (float.IsFinite(size._height) && size._height > maxIdealHeight2) maxIdealHeight2 = size._height;
		}

		float maxTotalWidth2 = maxTotalWidth;
		float maxTotalHeight2 = maxTotalHeight;
		if (Orientation == Orientation.Vertical)
		{
			if (maxIdealWidth2 < maxTotalWidth2) maxTotalWidth2 = maxIdealWidth2;
			maxTotalHeight2 = maxIdealHeight2 * context.Children.Count;
			if (maxTotalHeight2 > maxTotalHeight) maxTotalHeight2 = maxTotalHeight;
		}
		else
		{
			if (maxIdealHeight2 < maxTotalHeight2) maxTotalHeight2 = maxIdealHeight2;
			maxTotalWidth2 = maxIdealWidth2 * context.Children.Count;
			if (maxTotalWidth2 > maxTotalWidth) maxTotalWidth2 = maxTotalWidth;
		}

		return new(maxTotalWidth2, maxTotalHeight2);
	}

	protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
	{
		var children = context.Children;
		if (children.Count == 0) return new Size(0, 0);
		float width = float.IsFinite(finalSize._width) ? finalSize._width : 0;
		float height = float.IsFinite(finalSize._height) ? finalSize._height : 0;

		if (Orientation == Orientation.Vertical)
		{
			float h = height / children.Count;

			for (int i = 0; i < children.Count; i++)
			{
				var child = children[i];

				child.Arrange(new Rect(0, i * h, width, h));
			}
		}
		else
		{
			float w = width / children.Count;

			for (int i = 0; i < children.Count; i++)
			{
				var child = children[i];

				child.Arrange(new Rect(i * w, 0, w, height));
			}
		}

		return new(width, height);
	}
}
