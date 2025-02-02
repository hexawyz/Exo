namespace Exo.Images;

/// <summary>Describe known supported image formats.</summary>
[Flags]
public enum ImageFormats : uint
{
	// NB: Enum must be kept in sync with ImageFormats
	Raw = 0b00000001,
	Bitmap = 0b00000010,
	Gif = 0b00000100,
	Jpeg = 0b00001000,
	Png = 0b00010000,
	WebPLossy = 0b00100000,
	WebPLossless = 0b01000000,
}
