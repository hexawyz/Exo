namespace Exo;

[TypeId(0x0BB0B78A, 0xBEE5, 0x4A53, 0x90, 0x0E, 0x1F, 0x1A, 0x09, 0x1C, 0x3A, 0x00)]
public abstract class Image
{
	public abstract uint Width { get; }
	public abstract uint Height { get; }

	public abstract Memory<byte> GetRawBytes();
	public abstract Memory<byte> GetBitmapBytes();
	public abstract Memory<byte> GetJpegBytes();
	public abstract Memory<byte> GetPngBytes();
}
