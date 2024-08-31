using Exo.Ui;
using Windows.UI;

namespace Exo.Settings.Ui.Services;

public sealed class EditionService : BindableObject, IEditionService
{
	private Color _color = Color.FromArgb(255, 255, 255, 255);
	private bool _showToolbar = true;

	public Color Color
	{
		get => _color;
		set => SetValue(ref _color, value, ChangedProperty.Color);
	}

	public bool ShowToolbar
	{
		get => _showToolbar;
		set => SetValue(ref _showToolbar, value, ChangedProperty.ShowToolbar);
	}
}
