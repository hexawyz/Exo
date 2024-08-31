using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Exo.Configuration;

public sealed class AssemblyNameSerializer : INameSerializer<AssemblyName>
{
	public static AssemblyNameSerializer Instance = new();

	private AssemblyNameSerializer() { }

	public string FileNamePattern => "*, Version=*, Culture=*, PublicKeyToken=*";

	public AssemblyName Parse(ReadOnlySpan<char> text) => new(text.ToString());

	public bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out AssemblyName? result)
	{
		try
		{
			result = new(text.ToString());
			return true;
		}
		catch
		{
			result = null;
			return false;
		}
	}

	public string ToString(AssemblyName value) => value.FullName;
}
