using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Exo.Configuration;

public sealed class UInt128NameSerializer : INameSerializer<UInt128>
{
	public static UInt128NameSerializer Instance = new();

	private UInt128NameSerializer() { }

	public string FileNamePattern => "????????????????????????????????";

	public UInt128 Parse(ReadOnlySpan<char> text) => UInt128.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
	public bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out UInt128 result) => UInt128.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
	public string ToString(UInt128 value) => value.ToString("X32", CultureInfo.InvariantCulture);
}
