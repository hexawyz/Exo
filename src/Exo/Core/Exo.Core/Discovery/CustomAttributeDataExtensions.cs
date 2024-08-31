using System.Collections.Immutable;
using System.Reflection;

namespace Exo.Discovery;

public static class CustomAttributeDataExtensions
{
	public static CustomAttributeData? FirstOrDefault<T>(this ImmutableArray<CustomAttributeData> array)
		=> array.FirstOrDefault(d => TypeExtensions.Matches<T>(d.AttributeType));

	public static bool Matches<T>(this CustomAttributeData attribute)
		=> TypeExtensions.Matches<T>(attribute.AttributeType);
}
