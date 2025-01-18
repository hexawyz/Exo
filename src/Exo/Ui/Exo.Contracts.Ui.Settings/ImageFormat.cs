using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum ImageFormat : uint
{
	Raw = 0,
	Bitmap = 1,
	Gif = 2,
	Jpeg = 3,
	Png = 4,
	WebP = 5,
}
