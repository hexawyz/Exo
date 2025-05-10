using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Exo.Utils;

public static class GitCommitHelper
{
	private static readonly SearchValues<char> HexadecimalCharacters = SearchValues.Create("0123456789ABCDEFabcdef");

	public static ImmutableArray<byte> GetCommitId(string fileName)
		=> ParseCommitId(FileVersionInfo.GetVersionInfo(fileName).ProductVersion);

	public static ImmutableArray<byte> GetCommitId(Assembly assembly)
		=> assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is { } attr ?
			GetCommitId(attr) :
			[];

	public static ImmutableArray<byte> GetCommitId(AssemblyInformationalVersionAttribute assemblyInformationalVersionAttribute)
		=> assemblyInformationalVersionAttribute.InformationalVersion is { } informationalVersion ?
			ParseCommitId(informationalVersion) :
			[];

	private static ImmutableArray<byte> ParseCommitId(string? informationalVersion)
		=> informationalVersion is not null && informationalVersion.IndexOf('+') is >= 0 and int separatorIndex ?
			ValidateSha1(informationalVersion.AsSpan(separatorIndex + 1)) :
			[];

	private static ImmutableArray<byte> ValidateSha1(ReadOnlySpan<char> version)
		=> version.Length == 40 && version.IndexOfAnyExcept(HexadecimalCharacters) < 0 ? ImmutableCollectionsMarshal.AsImmutableArray(Convert.FromHexString(version)) : [];
}
