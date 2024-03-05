using System.Diagnostics.CodeAnalysis;

namespace Exo.Configuration;

public sealed class GuidNameSerializer : INameSerializer<Guid>
{
	public static GuidNameSerializer Instance = new();

	private GuidNameSerializer() { }

	public string FileNamePattern => "????????-????-????-????-????????????";

	public Guid Parse(ReadOnlySpan<char> text) => Guid.ParseExact(text, "D");
	public bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out Guid result) => Guid.TryParseExact(text, "D", out result);
	public string ToString(Guid value) => value.ToString("D");
}
