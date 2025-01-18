using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Exo.Configuration;

public sealed class ImageNameSerializer : INameSerializer<string>
{
	private static readonly SearchValues<char> AllowedCharacters = SearchValues.Create("+-0123456789=ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");

	public static bool IsNameValid(ReadOnlySpan<char> name) => !name.ContainsAnyExcept(AllowedCharacters);

	public static ImageNameSerializer Instance = new();

	private ImageNameSerializer() { }

	public string FileNamePattern => "*";

	public string Parse(ReadOnlySpan<char> text)
	{
		if (!IsNameValid(text)) throw new ArgumentException("Invalid character.");
		return text.ToString();
	}

	public bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out string? result)
	{
		if (!IsNameValid(text))
		{
			result = null;
			return false;
		}
		result = text.ToString();
		return true;
	}

	public string ToString(string value) => value;
}
