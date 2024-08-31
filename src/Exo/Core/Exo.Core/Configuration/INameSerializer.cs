using System.Diagnostics.CodeAnalysis;

namespace Exo.Configuration;

public interface INameSerializer<T>
{
	string FileNamePattern => "*";

	T Parse(ReadOnlySpan<char> text);
	bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out T? result);
	string ToString(T value);
}
