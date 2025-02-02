namespace Exo.Images;

/// <summary>Describe know supported image formats.</summary>
public enum ImageFormat : byte
{
	// NB: Enum must be kept in sync with ImageFormats
	Raw = 0,
	Bitmap = 1,
	Gif = 2,
	Jpeg = 3,
	Png = 4,
	WebPLossy = 5,
	WebPLossless = 6,
}
