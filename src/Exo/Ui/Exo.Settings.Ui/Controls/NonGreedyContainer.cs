using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Exo.Settings.Ui.Controls;

internal sealed partial class NonGreedyScrollViewer : ContentControl
{
	private Size? _finalSize;

	public NonGreedyScrollViewer()
	{
		HorizontalContentAlignment = HorizontalAlignment.Stretch;
		VerticalAlignment = VerticalAlignment.Stretch;
		SizeChanged += OnSizeChanged;
	}

	private void OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		InvalidateMeasure();
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (_finalSize is { } finalSize)
		{
			return base.MeasureOverride(_finalSize.GetValueOrDefault());
		}
		else if (float.IsPositiveInfinity(availableSize._height))
		{
			var maxSize = base.MeasureOverride(availableSize);
			double minHeight = MinHeight;
			var minSize = base.MeasureOverride(new(availableSize._width, double.IsNaN(minHeight) || minHeight <= 0 ? 1 : minHeight));
			return new(maxSize._width, Math.Min(maxSize._height, minSize._height));
		}
		return base.MeasureOverride(availableSize);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		if (_finalSize is null)
		{
			_finalSize = finalSize;
			InvalidateMeasure();
			return base.ArrangeOverride(finalSize);
		}
		else
		{
			_finalSize = null;
			return base.ArrangeOverride(finalSize);
		}
	}
}
