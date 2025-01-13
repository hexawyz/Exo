namespace Exo.Images;

/// <summary>Describe known supported image formats.</summary>
[Flags]
public enum ImageFormats
{
	// NB: Enum must be kept in sync with ImageFormats
	Raw = 0x00000001,
	Bitmap = 0x00000010,
	Gif = 0x00000100,
	Jpeg = 0x00001000,
	Png = 0x00010000,
}
