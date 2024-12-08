using System.Diagnostics.CodeAnalysis;

namespace DeviceTools;

public static partial class Properties
{
#if !NETSTANDARD2_0
	internal static bool TryGetByName(string name, [NotNullWhen(true)] out Property? property) =>
#else
	internal static bool TryGetByName(string name, out Property? property) =>
#endif
		Data.PropertiesByName.TryGetValue(name, out property!);

#if !NETSTANDARD2_0
	internal static bool TryGetName(PropertyKey key, [NotNullWhen(true)] out string? name) =>
#else
	public static bool TryGetName(PropertyKey key, out string? name) =>
#endif
		Data.PropertyNames.TryGetValue(key, out name);

#if !NETSTANDARD2_0
	internal static bool TryGetByKey(PropertyKey key, [NotNullWhen(true)] out Property? property)
#else
	public static bool TryGetByKey(PropertyKey key, out Property? property)
#endif
	{
		if (Data.PropertyNames.TryGetValue(key, out var name))
		{
			return Data.PropertiesByName.TryGetValue(name, out property);
		}
		else
		{
			property = null;
			return false;
		}
	}
}
