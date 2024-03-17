using Exo.Contracts;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class RgbColorFixedLengthArrayPropertyViewModel : FixedLengthArrayPropertyViewModel
{
	public RgbColorFixedLengthArrayPropertyViewModel(ConfigurablePropertyInformation propertyInformation)
		: base(propertyInformation, Color.FromArgb(255, 0, 0, 0))
	{
	}

	protected override int ItemSize => 3;

	protected override object? ReadValue(ReadOnlySpan<byte> source) => new Color { };

	protected override void WriteValue(Span<byte> destination, object? value) => throw new NotImplementedException();

	protected internal override bool AreValuesEqual(object? a, object? b)
		=> ReferenceEquals(a, b) || a is Color colorA && b is Color colorB && colorA == colorB;
}
