using Exo.Programming.Annotations;

namespace Exo.Service;

[Module("Image")]
[TypeId(0x718FB272, 0x914C, 0x43E5, 0x85, 0x5C, 0x2E, 0x91, 0x49, 0xBC, 0x28, 0xB3)]
public sealed class ImageService
{
	public Image Load(string path)
	{
		return null!;
	}

	public Image Rectangle(int x, int y, int width, int height)
	{
		return null!;
	}
}

public sealed class ImageSharpImage : Image
{
	public override uint Width { get; }
	public override uint Height { get; }

	public override Memory<byte> GetRawBytes() => throw new NotImplementedException();
	public override Memory<byte> GetBitmapBytes() => throw new NotImplementedException();
	public override Memory<byte> GetJpegBytes() => throw new NotImplementedException();
	public override Memory<byte> GetPngBytes() => throw new NotImplementedException();
}
