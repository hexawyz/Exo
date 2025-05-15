using System.Text.Json.Serialization;
using Exo.Images;

namespace Exo.Service.Configuration;

[TypeId(0x1D185C1A, 0x4903, 0x4D4A, 0x91, 0x20, 0x69, 0x4A, 0xE5, 0x2C, 0x07, 0x7A)]
[method: JsonConstructor]
internal readonly struct ImageMetadata(UInt128 id, ushort width, ushort height, ImageFormat format, bool isAnimated)
{
	public UInt128 Id { get; } = id;
	public ushort Width { get; } = width;
	public ushort Height { get; } = height;
	public ImageFormat Format { get; } = format;
	public bool IsAnimated { get; } = isAnimated;
}
