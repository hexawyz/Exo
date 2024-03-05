using System.Collections.Concurrent;
using System.Reflection;

namespace Exo.Service;

public sealed class AssemblyParsedDataCache<T> : IAssemblyParsedDataCache<T>
{
	private readonly IAssemblyLoader _assemblyLoader;
	private readonly ConcurrentDictionary<AssemblyName, T> _cache = new();

	public AssemblyParsedDataCache(IAssemblyLoader assemblyLoader) => _assemblyLoader = assemblyLoader;

	public IEnumerable<KeyValuePair<AssemblyName, T>> EnumerateAll()
	{
		foreach (var assemblyName in _assemblyLoader.AvailableAssemblies)
		{
			if (_cache.TryGetValue(assemblyName, out var value))
			{
				yield return new KeyValuePair<AssemblyName, T>(assemblyName, value);
			}
		}
	}

	public bool TryGetValue(AssemblyName assemblyName, out T? value)
		=> _cache.TryGetValue(assemblyName, out value);

	public void SetValue(AssemblyName assemblyName, T value)
		=> _cache[assemblyName] = value;
}
