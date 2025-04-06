using Exo.Contracts;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class RgbColorFixedLengthArrayPropertyViewModel : FixedLengthArrayPropertyViewModel
{
	public RgbColorFixedLengthArrayPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength)
		: base(propertyInformation, paddingLength, Color.FromArgb(255, 0, 0, 0))
	{
	}

	protected override int ItemSize => 3;

	protected override object? ReadValue(ReadOnlySpan<byte> source) => Color.FromArgb(255, source[0], source[1], source[2]);

	protected override void WriteValue(Span<byte> destination, object? value)
	{
		ArgumentNullException.ThrowIfNull(value);
		var color = (Color)value;
		destination[0] = color.R;
		destination[1] = color.G;
		destination[2] = color.B;
	}

	protected internal override bool AreValuesEqual(object? a, object? b)
		=> ReferenceEquals(a, b) || a is Color colorA && b is Color colorB && colorA == colorB;
}
