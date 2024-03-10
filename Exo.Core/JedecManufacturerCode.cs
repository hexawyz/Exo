using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Exo;

[StructLayout(LayoutKind.Sequential)]
public readonly struct JedecManufacturerCode : IEquatable<JedecManufacturerCode>
{
	private readonly byte _bankNumberWithParity;
	private readonly byte _code;

	/// <summary>The manufacturer code bank. (Also number of continuation codes)</summary>
	/// <remarks>
	/// <para>This value does not include the parity bit. Please use <see cref=" SpdManufacturerIdCode"/> to retrieve the value containing the parity bits.</para>
	/// <para>
	/// Originally, manufacturer codes were exposed as a variable length byte string, where the sequence would be a variable number of continuation codes (The value <c>7F</c>),
	/// followed by the actual value of the manufacturer index. This has been simplified into a bank index which is the number of continuation bytes if the code was expressed in the legacy format.
	/// </para>
	/// </remarks>
	public byte BankNumber => (byte)(_bankNumberWithParity & 0x7F);

	/// <summary>The manufacturer index in the corresponding code page.</summary>
	/// <remarks>
	/// <para>This value does not include the parity bit. Please use <see cref=" SpdManufacturerIdCode"/> to retrieve the value containing the parity bits.</para>
	/// </remarks>
	public byte ManufacturerIndex => (byte)(_code & 0x7F);

	/// <summary>The manufacturer code as read from the DDR4 SPD format.</summary>
	/// <remarks>
	/// <para>
	/// The DDR4 SPD specification mandates that the value is to be interpreted as little-endian, where the LSB is <see cref="ContinuationCodeCount"/> and the MSB is <see cref="ManufacturerIndex"/>.
	/// Unlike <see cref="ManufacturerIndex"/> and <see cref="ContinuationCodeCount"/>, the two bytes returned here include the parity bit.
	/// </para>
	/// <para>This value would also be present as-is in the Memory Device structure of SMBIOS 3.2+.</para>
	/// </remarks>
	public ushort SpdManufacturerIdCode
		=> BitConverter.IsLittleEndian ?
			Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in _bankNumberWithParity)) :
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in _bankNumberWithParity)));

	/// <summary>Creates the structure from a raw value.</summary>
	/// <param name="manufacturerCode"></param>
	/// <returns></returns>
	public static JedecManufacturerCode FromRawValue(ushort manufacturerCode)
	{
		if (IsEven((byte)manufacturerCode) || IsEven((byte)(manufacturerCode >> 8))) throw new ArgumentException("Invalid parity.");
		if ((byte)manufacturerCode == 0x7F) throw new ArgumentException("Invalid manufacturer code.");

		return new(BitConverter.IsLittleEndian ? manufacturerCode : BinaryPrimitives.ReverseEndianness(manufacturerCode));
	}

	public static JedecManufacturerCode FromRawValues(byte bankNumberWithParity, byte manufacturerIndexWithParity)
	{
		if (IsEven(bankNumberWithParity) || IsEven(manufacturerIndexWithParity)) throw new ArgumentException("Invalid parity.");
		if (manufacturerIndexWithParity == 0x7F) throw new ArgumentException("Invalid manufacturer code.");

		return new(BitConverter.IsLittleEndian ? (byte)(manufacturerIndexWithParity << 8 | bankNumberWithParity) : (byte)(bankNumberWithParity << 8 | manufacturerIndexWithParity));
	}

	private static bool IsEven(byte value) => (BitOperations.PopCount(value) & 1) == 0;
	private static bool IsOdd(byte value) => (BitOperations.PopCount(value) & 1) != 0;

	[JsonConstructor]
	public JedecManufacturerCode(byte bankNumber, byte manufacturerIndex)
	{
		// Validate that the parity bit is unset.
		if ((sbyte)bankNumber < 0) throw new ArgumentOutOfRangeException(nameof(bankNumber));
		if ((sbyte)manufacturerIndex < 0) throw new ArgumentOutOfRangeException(nameof(manufacturerIndex));

		_bankNumberWithParity = (byte)(bankNumber | (IsEven(bankNumber) ? 0x80 : 0x00));
		_code = (byte)(manufacturerIndex | (IsEven(manufacturerIndex) ? 0x80 : 0x00));
	}

	private JedecManufacturerCode(ushort rawValue) => this = Unsafe.As<ushort, JedecManufacturerCode>(ref rawValue);

	public bool IsDefault => Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in _bankNumberWithParity)) == 0;

	public override bool Equals(object? obj) => obj is JedecManufacturerCode manufacturer && Equals(manufacturer);
	public bool Equals(JedecManufacturerCode other) => _bankNumberWithParity == other._bankNumberWithParity && _code == other._code;
	public override int GetHashCode() => HashCode.Combine(_bankNumberWithParity, _code);

	public static bool operator ==(JedecManufacturerCode left, JedecManufacturerCode right) => left.Equals(right);
	public static bool operator !=(JedecManufacturerCode left, JedecManufacturerCode right) => !(left == right);

	public override string? ToString()
	{
		if (IsDefault) return null;

		return string.Create
		(
			4,
			this,
			(span, mc) =>
			{
				mc._bankNumberWithParity.TryFormat(span[..2], out _, "X2", CultureInfo.InvariantCulture);
				mc._code.TryFormat(span[2..4], out _, "X2", CultureInfo.InvariantCulture);
			}
		);
	}
}
