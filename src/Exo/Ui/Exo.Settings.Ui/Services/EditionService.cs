using Exo.Ui;
using Windows.UI;
using WinRT;

namespace Exo.Settings.Ui.Services;

[GeneratedBindableCustomProperty]
public sealed partial class EditionService : BindableObject, IEditionService
{
	private Color _color = Color.FromArgb(255, 255, 255, 255);

	public Color Color
	{
		get => _color;
		set => SetValue(ref _color, value, ChangedProperty.Color);
	}
}
