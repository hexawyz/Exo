using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace DeviceTools.Firmware;

[StructLayout(LayoutKind.Sequential)]
public readonly struct JedecManufacturerId : IEquatable<JedecManufacturerId>, IFormattable
{
	private readonly byte _continuationCodeCountWithParity;
	private readonly byte _codeWithParity;

	[JsonConstructor]
	public JedecManufacturerId(byte continuationCodeCountWithParity, byte codeWithParity)
	{
		_continuationCodeCountWithParity = continuationCodeCountWithParity;
		_codeWithParity = codeWithParity;
	}

	/// <summary>The number of continuation codes before the final code.</summary>
	/// <remarks>
	/// <para>This value does not include the parity bit. Please use <see cref=" SpdManufacturerIdCode"/> to retrieve the value containing the parity bits.</para>
	/// <para>
	/// Originally, manufacturer codes were exposed as a variable length byte string, where the sequence would be a variable number of continuation codes (The value <c>7F</c>),
	/// followed by the actual value of the manufacturer index. This has been simplified into a bank index which is the number of continuation bytes if the code was expressed in the legacy format.
	/// </para>
	/// </remarks>
	public byte ContinuationCodeCount => (byte)(_continuationCodeCountWithParity & 0x7F);

	/// <summary>The manufacturer code in the corresponding code page.</summary>
	/// <remarks>
	/// <para>This value does not include the parity bit. Please use <see cref=" SpdManufacturerIdCode"/> to retrieve the value containing the parity bits.</para>
	/// </remarks>
	public byte Code => (byte)(_codeWithParity & 0x7F);

	/// <summary>The manufacturer code bank.</summary>
	/// <remarks>
	/// <para>This is derived from <see cref="ContinuationCodeCount"/>, where the bank number is the value incremented by <c>#1/c>.</para>
	/// </remarks>
	public byte BankNumber => (byte)((_continuationCodeCountWithParity & 0x7F) + 1);

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
			Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in _continuationCodeCountWithParity)) :
			BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in _continuationCodeCountWithParity)));

	private static bool IsEven(byte value) => (BitOperations.PopCount(value) & 1) == 0;
	private static bool IsOdd(byte value) => (BitOperations.PopCount(value) & 1) != 0;

	public bool IsDefault => (ContinuationCodeCount | Code) == 0;
	public bool IsParityValid => IsOdd(ContinuationCodeCount) || IsOdd(Code);

	public JedecManufacturerId FixParity()
		=> new JedecManufacturerId(IsEven(ContinuationCodeCount) ? (byte)(0x80 | ContinuationCodeCount) : ContinuationCodeCount, IsEven(Code) ? (byte)(0x80 | Code) : Code);

	public override string? ToString()
	{
		if (IsDefault) return null;

		return string.Create
		(
			4,
			FixParity(),
			(span, mc) =>
			{
				mc.ContinuationCodeCount.TryFormat(span[..2], out _, "X2", CultureInfo.InvariantCulture);
				mc.Code.TryFormat(span[2..4], out _, "X2", CultureInfo.InvariantCulture);
			}
		);
	}

	public string ToString(string? format, IFormatProvider? formatProvider) => ToString()!;

	public override bool Equals(object? obj) => obj is JedecManufacturerId code && Equals(code);
	public bool Equals(JedecManufacturerId other) => ContinuationCodeCount == other.ContinuationCodeCount && Code == other.Code;
	public override int GetHashCode() => HashCode.Combine(ContinuationCodeCount, Code);

	public static bool operator ==(JedecManufacturerId left, JedecManufacturerId right) => left.Equals(right);
	public static bool operator !=(JedecManufacturerId left, JedecManufacturerId right) => !(left == right);

}
