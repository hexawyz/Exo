using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
public sealed partial class DpiViewModel
{
	private readonly DotsPerInch _dpi;

	public DpiViewModel(DotsPerInch dpi) => _dpi = dpi;

	public ushort Horizontal => _dpi.Horizontal;
	public ushort Vertical => _dpi.Vertical;

	public override string ToString()
	{
		if (Horizontal == Vertical) return Horizontal.ToString();
		else return $"{Horizontal}x{Vertical}";
	}
}
