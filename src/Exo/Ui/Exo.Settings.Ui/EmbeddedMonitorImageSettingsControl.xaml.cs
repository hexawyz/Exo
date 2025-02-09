using System.Collections.Immutable;
using System.ComponentModel;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui;

internal sealed partial class EmbeddedMonitorImageSettingsControl : UserControl
{
	// For some reason, XAML won't do any type check and will mess up everything by assigning the wrong type here sometimes…
	public EmbeddedMonitorImageGraphicsViewModel? ImageGraphics
	{
		get => (EmbeddedMonitorImageGraphicsViewModel)GetValue(ImageGraphicsProperty);
		set => SetValue(ImageGraphicsProperty, value);
	}

	public static readonly DependencyProperty ImageGraphicsProperty = DependencyProperty.Register
	(
		nameof(ImageGraphics),
		typeof(EmbeddedMonitorImageGraphicsViewModel),
		typeof(EmbeddedMonitorImageSettingsControl),
		new PropertyMetadata(null, (d, e) => ((EmbeddedMonitorImageSettingsControl)d).OnPropertyChanged(e))
	);

	public EmbeddedMonitorImageSettingsControl()
	{
		InitializeComponent();
		ImageCropper.RegisterPropertyChangedCallback(CommunityToolkit.WinUI.Controls.ImageCropper.SourceProperty, OnImageCropperPropertyChanged);
	}

	private void OnImageCropperLoaded(object sender, RoutedEventArgs e)
	{
		TryBindPointerEvents();
	}

	private void TryBindPointerEvents()
	{
		// This is hacky but seemingly the only good way to propagate a change.
		var children = FindChildren
		(
			ImageCropper,
			[
				"PART_SourceImage",
				"PART_TopThumb",
				"PART_BottomThumb",
				"PART_LeftThumb",
				"PART_RightThumb",
				"PART_UpperLeftThumb",
				"PART_UpperRightThumb",
				"PART_LowerLeftThumb",
				"PART_LowerRightThumb",
			]
		);
		foreach (var child in children)
		{
			BindThumbEvents(child);
		}
	}

	private void BindThumbEvents(FrameworkElement element)
	{
		element.ManipulationCompleted += OnImageCropperThumbManipulationCompleted;
	}

	private void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.Property == ImageGraphicsProperty)
		{
			if (e.OldValue is EmbeddedMonitorImageGraphicsViewModel oldValue) oldValue.PropertyChanged -= OnImageGraphicsPropertyChanged;
			if (e.NewValue is EmbeddedMonitorImageGraphicsViewModel newValue) newValue.PropertyChanged += OnImageGraphicsPropertyChanged;
			UpdateCroppedRegionFromGraphics();
		}
	}

	private void OnImageCropperPropertyChanged(DependencyObject sender, DependencyProperty dp)
	{
		if (dp == CommunityToolkit.WinUI.Controls.ImageCropper.SourceProperty)
		{
			// Quick and dirty fix to be sure that the cropping region is properly synchronized when we switch images.
			UpdateCroppedRegionToGraphics();
		}
	}

	private void OnImageGraphicsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (BindableObject.Equals(e, ChangedProperty.CropRectangle))
		{
			UpdateCroppedRegionFromGraphics();
		}
	}

	private void UpdateCroppedRegionFromGraphics()
	{
		if (ImageGraphics is { } imageGraphics)
		{
			var rectangle = imageGraphics.CropRectangle;

			ImageCropper.TrySetCroppedRegion(new(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height));
		}
	}

	private void UpdateCroppedRegionToGraphics()
	{
		if (ImageGraphics is { } imageGraphics)
		{
			var rectangle = ImageCropper.CroppedRegion;
			if (rectangle._x < 0 || rectangle._x > int.MaxValue ||
				rectangle._y < 0 || rectangle._y > int.MaxValue ||
				rectangle._width < 0 || rectangle._width > int.MaxValue ||
				rectangle._height < 0 || rectangle._height > int.MaxValue)
			{
				return;
			}

			var image = imageGraphics.Image;

			// Resolve the aspect ratio thingy here. We want to truncate the coordinates into integers.
			// NB: The Math.Round here is used to mitigate weird behavior from the ImageCropper. (Otherwise we would continuously alter the region size as it is moved…)
			int left = (int)Math.Round(rectangle.Left);
			int top = (int)Math.Round(rectangle.Top);
			int width = (int)Math.Round(rectangle.Width);
			int height = (int)Math.Round(rectangle.Height);

			// We need to check that the aspect ratio is actually correct.
			if (image is not null && width > 0 && height > 0)
			{
				double actualAspectRatio = (double)width / height;
				double desiredAspectRatio = imageGraphics.AspectRatio;
				if (desiredAspectRatio > 0 && actualAspectRatio != desiredAspectRatio)
				{
					// In case the aspect ratio does not match, we have to make if work by adjusting the region by the smallest possible amount to make it fit.
					// We should generally have two choices: A slightly larger region or a slightly smaller region. Sometimes, one of those would be invalid (too small or too big for the image)
					// Choosing between the two is a matter of determining which one implies the smallest difference in the number of pixels.
					// By default, the region will be inflated or deflated relative to the center, but obviously, dimensions will have to match.
					// NB: Actually for now, let's just check the smaller region all the time. The larger one would always be (W+P)x(H+Q) but the check needs to be done on the dimension showing the smallest diff.

					// Basically, determining the two WxH candidates would be the simplest by first identifying the smallest P/Q fraction corresponding to the requested aspect ratio.
					// This is easily done by computing the GCD.

					uint gcd = MathUtils.Gcd((uint)imageGraphics.ImageSize.Width, (uint)imageGraphics.ImageSize.Height);
					uint p = (uint)imageGraphics.ImageSize.Width / gcd;
					uint q = (uint)imageGraphics.ImageSize.Height / gcd;

					// Now that we know the P/Q numbers, we can easily check how far off we are in each direction.
					// Each dimension must be a multiple of their corresponding number.

					var (w, h) = GetMinimumMatchingArea((uint)width, (uint)height, p, q);

					if (w <= image.Width && h < image.Height)
					{
						uint dw = (uint)width - w;
						uint hw = dw >>> 1;

						uint dh = (uint)height - h;
						uint hh = dh >>> 1;

						// As an arbitrary rule, if we need to adjust by an odd number, we always choose to alter more the right/bottom side when possible.

						if (w < (uint)width) left += (int)hw;
						else if ((uint)left <= hw) left = 0;
						else if (image.Width - ((uint)width + (uint)left) < hw) left -= (int)(dw - hw);
						else left -= (int)hw;
						width = (int)w;

						if (h < (uint)height) top += (int)hh;
						else if ((uint)top <= hh) top = 0;
						else if (image.Height - ((uint)height + (uint)top) < hh) top -= (int)(dh - hh);
						else top -= (int)hh;
						height = (int)h;
					}
				}
			}

			imageGraphics.CropRectangle = new() { Left = left, Top = top, Width = width, Height = height };
		}
	}

	// This always return the smallest possible area matching the dimension. Minimum possible result is (p, q).
	private static (uint a, uint b) GetMinimumMatchingArea(uint a, uint b, uint p, uint q)
	{
		// TODO: ra and rb should be usable to determine the closest size instead of the smallest size.
		// We could actually augment the algorithm here with the max values for a and b so that we already guarantee to never exceed what is requested.
		var (qa, ra) = Math.DivRem(a, p);
		var (qb, rb) = Math.DivRem(b, q);

		uint n = Math.Max(1, Math.Min(qa, qb));

		return (n * p, n * q);
	}

	private void OnImageCropperThumbManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
		=> UpdateCroppedRegionToGraphics();

	private void OnImageCropperKeyUp(object sender, KeyRoutedEventArgs e)
		=> UpdateCroppedRegionToGraphics();

	private static ImmutableArray<FrameworkElement> FindChildren(FrameworkElement element, HashSet<string> names)
	{
		int childCount = VisualTreeHelper.GetChildrenCount(element);
		if (childCount > 0)
		{
			var children = new List<FrameworkElement>();
			FindChildren(element, childCount, names, children);
			return [.. children];
		}
		return [];
	}

	private static void FindChildren(FrameworkElement element, int childCount, HashSet<string> names, List<FrameworkElement> children)
	{
		for (int i = 0; i < childCount; i++)
		{
			if (VisualTreeHelper.GetChild(element, i) is not FrameworkElement child) continue;
			if (child.Name is not null && names.Contains(child.Name))
			{
				children.Add(child);
			}
			int count = VisualTreeHelper.GetChildrenCount(child);
			if (count > 0) FindChildren(child, count, names, children);
		}
	}
}
