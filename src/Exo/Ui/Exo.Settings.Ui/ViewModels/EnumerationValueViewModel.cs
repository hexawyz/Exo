using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class EnumerationValueViewModel
{
	public EnumerationValueViewModel(string displayName, object value)
	{
		DisplayName = displayName;
		Value = value;
	}

	public string DisplayName { get; }
	public object Value { get; }
}
