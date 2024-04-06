using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;
using SixLabors.ImageSharp.PixelFormats;

namespace Exo.Service;

internal static class ColorExtensions
{
	public static Bgra32 ToBgra32(this ArgbColor color) => Unsafe.BitCast<uint, Bgra32>(BinaryPrimitives.ReverseEndianness(Unsafe.BitCast<ArgbColor, uint>(color)));
}
