using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui.Controls;

internal class GlyphToggleButton : ToggleButton
{
	public string Glyph
	{
		get => (string)GetValue(GlyphProperty);
		set => SetValue(GlyphProperty, value);
	}

	public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(GlyphToggleButton), new PropertyMetadata("\uEB51"));

	public string CheckedGlyph
	{
		get => (string)GetValue(CheckedGlyphProperty);
		set => SetValue(CheckedGlyphProperty, value);
	}

	public static readonly DependencyProperty CheckedGlyphProperty = DependencyProperty.Register(nameof(CheckedGlyph), typeof(string), typeof(GlyphToggleButton), new PropertyMetadata("\uEB52"));

	public string IndeterminateGlyph
	{
		get => (string)GetValue(IndeterminateGlyphProperty);
		set => SetValue(IndeterminateGlyphProperty, value);
	}

	public static readonly DependencyProperty IndeterminateGlyphProperty = DependencyProperty.Register(nameof(IndeterminateGlyph), typeof(string), typeof(GlyphToggleButton), new PropertyMetadata("\uEA92"));

	public Brush CheckedForeground
	{
		get => (Brush)GetValue(CheckedForegroundProperty);
		set => SetValue(CheckedForegroundProperty, value);
	}

	public static readonly DependencyProperty CheckedForegroundProperty = DependencyProperty.Register(nameof(CheckedForeground), typeof(Brush), typeof(GlyphToggleButton), new PropertyMetadata(new SolidColorBrush()));
}
