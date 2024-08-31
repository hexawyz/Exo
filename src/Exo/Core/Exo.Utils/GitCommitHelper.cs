using System.Buffers;
using System.Reflection;

namespace Exo.Utils;

public static class GitCommitHelper
{
	private static readonly SearchValues<char> HexadecimalCharacters = SearchValues.Create("0123456789ABCDEFabcdef");

	public static string? GetCommitId(Assembly assembly)
		=> assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is { } attr ?
			GetCommitId(attr) :
			null;

	public static string? GetCommitId(AssemblyInformationalVersionAttribute assemblyInformationalVersionAttribute)
		=> assemblyInformationalVersionAttribute.InformationalVersion is { } informationalVersion ?
			GetCommitId(informationalVersion) :
			null;

	private static string? GetCommitId(string informationalVersion)
		=> informationalVersion.IndexOf('+') is >= 0 and int separatorIndex ?
			ValidateSha1(informationalVersion.AsSpan(separatorIndex + 1)) :
			null;

	private static string? ValidateSha1(ReadOnlySpan<char> version)
		=> version.Length == 40 && version.IndexOfAnyExcept(HexadecimalCharacters) < 0 ? version.ToString() : null;
}
