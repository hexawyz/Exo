namespace Exo.Images;

public readonly struct PixelComponentFormat : IEquatable<PixelComponentFormat>
{
	public static PixelComponentFormat Empty => default;
	public static PixelComponentFormat Color1Bit => new PixelComponentFormat(0x01);
	public static PixelComponentFormat Color2Bit => new PixelComponentFormat(0x02);
	public static PixelComponentFormat Color3Bit => new PixelComponentFormat(0x03);
	public static PixelComponentFormat Color4Bit => new PixelComponentFormat(0x04);
	public static PixelComponentFormat Color5Bit => new PixelComponentFormat(0x05);
	public static PixelComponentFormat Color6Bit => new PixelComponentFormat(0x06);
	public static PixelComponentFormat Color7Bit => new PixelComponentFormat(0x07);
	public static PixelComponentFormat Color8Bit => new PixelComponentFormat(0x08);
	public static PixelComponentFormat Color10Bit => new PixelComponentFormat(0x09);
	public static PixelComponentFormat Color12Bit => new PixelComponentFormat(0x0A);
	public static PixelComponentFormat Color16Bit => new PixelComponentFormat(0x0B);
	public static PixelComponentFormat Color16BitFloat => new PixelComponentFormat(0x1B);
	public static PixelComponentFormat Color32Bit => new PixelComponentFormat(0x0D);
	public static PixelComponentFormat Color32BitFloat => new PixelComponentFormat(0x1D);

	private static ReadOnlySpan<byte> BitsPerComponentTable => [0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 16, 24, 32, 48, 64];

	private readonly byte _rawValue;

	public byte BitsPerComponent => BitsPerComponentTable[_rawValue & 0xF];
	public bool IsFloatingPoint => (_rawValue & 0x10) != 0;
	public bool IsEmpty => _rawValue == 0;

	private PixelComponentFormat(byte rawValue) => _rawValue = rawValue;

	public override bool Equals(object? obj) => obj is PixelComponentFormat format && Equals(format);
	public bool Equals(PixelComponentFormat other) => _rawValue == other._rawValue;
	public override int GetHashCode() => HashCode.Combine(_rawValue);

	public static bool operator ==(PixelComponentFormat left, PixelComponentFormat right) => left.Equals(right);
	public static bool operator !=(PixelComponentFormat left, PixelComponentFormat right) => !(left == right);
}
