using System.ComponentModel;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class EmbeddedMonitorImageSettingsControl : UserControl
{
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
	}

	private void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.Property == ImageGraphicsProperty)
		{
			if (e.OldValue is EmbeddedMonitorImageGraphicsViewModel oldValue) oldValue.PropertyChanged -= OnImageGraphicsPropertyChanged;
			if (e.NewValue is EmbeddedMonitorImageGraphicsViewModel newValue) newValue.PropertyChanged += OnImageGraphicsPropertyChanged;
			UpdateCroppedRegion();
		}
	}

	private void OnImageGraphicsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (BindableObject.Equals(e, ChangedProperty.CropRectangle))
		{
			UpdateCroppedRegion();
		}
	}

	private void UpdateCroppedRegion()
	{
		if (ImageGraphics is { } imageGraphics)
		{
			var rectangle = imageGraphics.CropRectangle;

			ImageCropper.TrySetCroppedRegion(new(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height));
		}
	}
}
