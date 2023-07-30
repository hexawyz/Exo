using System.Text;

namespace Exo;

internal static class Naming
{
	public static bool StartsWithLowerCase(string name)
		=> Rune.IsLower(Rune.GetRuneAt(name, 0));

	public static bool StartsWithUpperCase(string name)
		=> Rune.IsUpper(Rune.GetRuneAt(name, 0));

	public static string MakeCamelCase(string pascalCase)
	{
		ArgumentNullException.ThrowIfNull(pascalCase);
		if (pascalCase.Length == 0) throw new ArgumentException($"The name is empty.", nameof(pascalCase));
		var firstRune = Rune.GetRuneAt(pascalCase, 0);
		if (pascalCase.Length == 0 || Rune.IsLower(firstRune) || !Rune.IsLetter(firstRune)) throw new ArgumentException($"The name {pascalCase} is not Pascal-cased.");
		var lowerCaseFirstRune = Rune.ToLowerInvariant(firstRune);

		return string.Create
		(
			pascalCase.Length - firstRune.Utf16SequenceLength + lowerCaseFirstRune.Utf16SequenceLength,
			pascalCase,
			(span, text) =>
			{
				var firstRune = Rune.GetRuneAt(text, 0);
				var lowerCaseFirstRune = Rune.ToLowerInvariant(firstRune);

				lowerCaseFirstRune.EncodeToUtf16(span);
				text.AsSpan(firstRune.Utf16SequenceLength).CopyTo(span[lowerCaseFirstRune.Utf16SequenceLength..]);
			}
		);
	}

	public static string MakePascalCase(string camelCase)
	{
		ArgumentNullException.ThrowIfNull(camelCase);
		if (camelCase.Length == 0) throw new ArgumentException($"The name is empty.", nameof(camelCase));
		var firstRune = Rune.GetRuneAt(camelCase, 0);
		if (camelCase.Length == 0 || Rune.IsUpper(firstRune) || !Rune.IsLetter(firstRune)) throw new ArgumentException($"The name {camelCase} is not camel-cased.");
		var upperCaseFirstRune = Rune.ToUpperInvariant(firstRune);

		return string.Create
		(
			camelCase.Length - firstRune.Utf16SequenceLength + upperCaseFirstRune.Utf16SequenceLength,
			camelCase,
			(span, text) =>
			{
				var firstRune = Rune.GetRuneAt(text, 0);
				var upperCaseFirstRune = Rune.ToUpperInvariant(firstRune);

				upperCaseFirstRune.EncodeToUtf16(span);
				text.AsSpan(firstRune.Utf16SequenceLength).CopyTo(span[upperCaseFirstRune.Utf16SequenceLength..]);
			}
		);
	}
}
