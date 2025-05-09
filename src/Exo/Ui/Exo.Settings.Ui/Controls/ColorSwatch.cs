using Exo.Settings.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Windows.UI;

namespace Exo.Settings.Ui.Controls;

/// <summary>Represents a control that displays a color and allows updates through interaction with the <see cref="IEditionService"/>.</summary>
/// <remarks>
/// When enabled, a left click on the control will update <see cref="Color"/> to the value of <see cref="IEditionService.Color"/>,
/// and a right click will copy <see cref="Color"/> into <see cref="IEditionService.Color"/>.
/// </remarks>
[ContentProperty(Name = nameof(Color))]
internal sealed class ColorSwatch : Control
{
	public Color Color
	{
		get => (Color)GetValue(ColorProperty);
		set => SetValue(ColorProperty, value);
	}

	public static readonly DependencyProperty ColorProperty = DependencyProperty.Register("Color", typeof(Color), typeof(ColorSwatch), new PropertyMetadata(Color.FromArgb(255, 255, 255, 255)));

	private readonly IEditionService _editionService;

	public ColorSwatch()
	{
		_editionService = App.Current.Services.GetRequiredService<IEditionService>();
	}

	protected override void OnPointerPressed(PointerRoutedEventArgs e)
	{
		base.OnPointerPressed(e);

		if (e.Handled || !IsEnabled) return;

		var point = e.GetCurrentPoint(this);

		if (point.Properties.IsLeftButtonPressed)
		{
			Color = _editionService.Color;
		}
		else if (point.Properties.IsRightButtonPressed)
		{
			_editionService.Color = Color;
		}
		e.Handled = true;
	}
}
