using Exo.ColorFormats;
using Exo.Lighting;

namespace Exo.Settings.Ui.ViewModels;

internal sealed partial class RgbColorArrayPropertyViewModel : ArrayPropertyViewModel<RgbColor, RgbColorArrayElementViewModel>
{
	public RgbColorArrayPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength)
		: base(propertyInformation, paddingLength, propertyInformation.DefaultValue is RgbColor[] colors ? colors : null, propertyInformation.DefaultValue is RgbColor color ? color : new RgbColor(255, 255, 255))
	{
	}

	protected override int ItemSize => 3;

	protected override RgbColor ReadValue(ReadOnlySpan<byte> source) => new(source[0], source[1], source[2]);

	protected override void WriteValue(Span<byte> destination, RgbColor value)
	{
		destination[0] = value.R;
		destination[1] = value.G;
		destination[2] = value.B;
	}

	protected override RgbColorArrayElementViewModel CreateElement(RgbColor value) => new(this, value);
}
