namespace Exo.Service;

internal static class HashSetExtensions
{
	public static void AddRange<T>(this HashSet<T> hashSet, T[]? values)
	{
		if (values is null) return;
		foreach (var value in values)
		{
			hashSet.Add(value);
		}
	}
}
