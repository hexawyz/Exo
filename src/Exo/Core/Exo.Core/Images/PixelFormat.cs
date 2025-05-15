using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.Images;

[JsonConverter(typeof(JsonConverter))]
public readonly struct PixelFormat : IEquatable<PixelFormat>
{
	public sealed class JsonConverter : JsonConverter<PixelFormat>
	{
		public override PixelFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.GetString() switch
			{
				nameof(PixelFormat.R8G8B8) => PixelFormat.R8G8B8,
				nameof(PixelFormat.B8G8R8) => PixelFormat.B8G8R8,
				nameof(PixelFormat.X8R8G8B8) => PixelFormat.X8R8G8B8,
				nameof(PixelFormat.X8B8G8R8) => PixelFormat.X8B8G8R8,
				nameof(PixelFormat.R8G8B8X8) => PixelFormat.R8G8B8X8,
				nameof(PixelFormat.B8G8R8X8) => PixelFormat.B8G8R8X8,
				nameof(PixelFormat.A8R8G8B8) => PixelFormat.A8R8G8B8,
				nameof(PixelFormat.A8B8G8R8) => PixelFormat.A8B8G8R8,
				nameof(PixelFormat.R8G8B8A8) => PixelFormat.R8G8B8A8,
				nameof(PixelFormat.B8G8R8A8) => PixelFormat.B8G8R8A8,
				_ => throw new InvalidOperationException(),
			};

		public override void Write(Utf8JsonWriter writer, PixelFormat value, JsonSerializerOptions options)
		{
			if (value == PixelFormat.R8G8B8) writer.WriteStringValue(nameof(PixelFormat.R8G8B8));
			else if (value == PixelFormat.B8G8R8) writer.WriteStringValue(nameof(PixelFormat.B8G8R8));
			else if (value == PixelFormat.X8R8G8B8) writer.WriteStringValue(nameof(PixelFormat.X8R8G8B8));
			else if (value == PixelFormat.X8B8G8R8) writer.WriteStringValue(nameof(PixelFormat.X8B8G8R8));
			else if (value == PixelFormat.R8G8B8X8) writer.WriteStringValue(nameof(PixelFormat.R8G8B8X8));
			else if (value == PixelFormat.B8G8R8X8) writer.WriteStringValue(nameof(PixelFormat.B8G8R8X8));
			else if (value == PixelFormat.A8R8G8B8) writer.WriteStringValue(nameof(PixelFormat.A8R8G8B8));
			else if (value == PixelFormat.A8B8G8R8) writer.WriteStringValue(nameof(PixelFormat.A8B8G8R8));
			else if (value == PixelFormat.R8G8B8A8) writer.WriteStringValue(nameof(PixelFormat.R8G8B8A8));
			else if (value == PixelFormat.B8G8R8A8) writer.WriteStringValue(nameof(PixelFormat.B8G8R8A8));
			else throw new InvalidOperationException();
		}
	}

	// As enumerating all possible color formats is a lost fight, we will instead use a compact system to describe pixel formats.
	// This representation is internal and as such, can evolve to fit more needs.

	// Supported bitness for each component: 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 16, 24, 32, 48, 64 (Hopefully that's exhaustive enough; it fits fine in 4 bits)
	// Number of components: 5; Disabled components must be 0, non empty components must be packed together.
	// Component format: Integer / Float (16 bits+)
	// Little endian: yes / no (For when components are split over multiple bytes; when components are 8 bits, this must never be yes)
	// Transparency: yes / no
	// Color systems: Palette, sRGB, RGB, CMYK, … (3bits reserved)
	// Permutations: (Normal order, Reversed)x(Alpha first, Alpha last)

	public static PixelFormat R8G8B8 => new(0b_00000_00000_01000_01000_01000_0_0_0_0_001);
	public static PixelFormat B8G8R8 => new(0b_00000_00000_01000_01000_01000_0_0_0_1_001);
	public static PixelFormat X8R8G8B8 => new(0b_00000_01000_01000_01000_01000_0_0_0_0_001);
	public static PixelFormat X8B8G8R8 => new(0b_00000_01000_01000_01000_01000_0_0_0_1_001);
	public static PixelFormat R8G8B8X8 => new(0b_00000_01000_01000_01000_01000_0_1_0_0_001);
	public static PixelFormat B8G8R8X8 => new(0b_00000_01000_01000_01000_01000_0_1_0_1_001);
	public static PixelFormat A8R8G8B8 => new(0b_00000_01000_01000_01000_01000_0_0_1_0_001);
	public static PixelFormat A8B8G8R8 => new(0b_00000_01000_01000_01000_01000_0_0_1_1_001);
	public static PixelFormat R8G8B8A8 => new(0b_00000_01000_01000_01000_01000_0_1_1_0_001);
	public static PixelFormat B8G8R8A8 => new(0b_00000_01000_01000_01000_01000_0_1_1_1_001);

	private readonly uint _rawValue;

	private PixelFormat(uint rawValue) => _rawValue = rawValue;

	/// <summary>Indicates the format of the first color component.</summary>
	/// <remarks>
	/// When the color format is palette, this is the palette index.
	/// When the color format is RGB, this is red.
	/// When the color format is CMYK, this is cyan.
	/// </remarks>
	public PixelComponentFormat Component1 => Unsafe.BitCast<byte, PixelComponentFormat>((byte)((_rawValue >>> 7) & 0x1F));
	/// <summary>Indicates the format of the second color component.</summary>
	/// <remarks>
	/// This must be unused if the color format is palette.
	/// When the color format is RGB, this is green.
	/// When the color format is CMYK, this is magenta.
	/// </remarks>
	public PixelComponentFormat Component2 => Unsafe.BitCast<byte, PixelComponentFormat>((byte)((_rawValue >>> 12) & 0x1F));
	/// <summary>Indicates the format of the third color component.</summary>
	/// <remarks>
	/// This must be unused if the color format is palette.
	/// When the color format is RGB, this is green.
	/// When the color format is CMYK, this is yellow.
	/// </remarks>
	public PixelComponentFormat Component3 => Unsafe.BitCast<byte, PixelComponentFormat>((byte)((_rawValue >>> 17) & 0x1F));
	/// <summary>Indicates the format of the fourth color component.</summary>
	/// <remarks>
	/// This must be unused if the color format is palette.
	/// When the color format is RGB, this can be alpha.
	/// When the color format is CMYK, this is black.
	/// </remarks>
	public PixelComponentFormat Component4 => Unsafe.BitCast<byte, PixelComponentFormat>((byte)((_rawValue >>> 22) & 0x1F));
	/// <summary>Indicates the format of the fifth color component.</summary>
	/// <remarks>
	/// This must be unused if the color format is not CMYK.
	/// When the color format is CMYK, this can be alpha.
	/// </remarks>
	public PixelComponentFormat Component5 => Unsafe.BitCast<byte, PixelComponentFormat>((byte)((_rawValue >>> 27) & 0x1F));

	/// <summary>Gets the number of color components that are defined.</summary>
	public uint ComponentCount => 5 - (uint)BitOperations.LeadingZeroCount(_rawValue) / 5;

	public ColorFormat ColorFormat => (ColorFormat)(_rawValue & 0x07);
	public bool IsComponentOrderReversed => (_rawValue & 0x08) != 0;
	public bool IsTransparent => (_rawValue & 0x10) != 0;
	public bool IsAlphaLast => (_rawValue & 0x20) == 0;
	public bool IsLittleEndian => (_rawValue & 0x40) != 0;

	public override bool Equals(object? obj) => obj is PixelFormat format && Equals(format);
	public bool Equals(PixelFormat other) => _rawValue == other._rawValue;
	public override int GetHashCode() => HashCode.Combine(_rawValue);

	public static bool operator ==(PixelFormat left, PixelFormat right) => left.Equals(right);
	public static bool operator !=(PixelFormat left, PixelFormat right) => !(left == right);
}
