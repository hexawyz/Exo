using System.Diagnostics;
using System.Globalization;

namespace Exo.Settings.Ui.ViewModels;

[DebuggerDisplay("{DisplayText,nq}")]
public sealed class PollingFrequencyViewModel
{
	public ushort Frequency { get; }
	public string DisplayText { get; }

	public PollingFrequencyViewModel(ushort frequency)
	{
		Frequency = frequency;
		DisplayText = string.Create(CultureInfo.InvariantCulture, $"{frequency}\xA0Hz");
	}
}
