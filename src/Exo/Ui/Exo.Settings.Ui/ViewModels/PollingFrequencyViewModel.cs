using System.Diagnostics;
using System.Globalization;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[DebuggerDisplay("{DisplayText,nq}")]
[GeneratedBindableCustomProperty]
internal sealed partial class PollingFrequencyViewModel
{
	public ushort Frequency { get; }
	public string DisplayText { get; }

	public PollingFrequencyViewModel(ushort frequency)
	{
		Frequency = frequency;
		DisplayText = string.Create(CultureInfo.InvariantCulture, $"{frequency}\xA0Hz");
	}
}
