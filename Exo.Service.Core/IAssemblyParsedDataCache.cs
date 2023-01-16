using System;
using System.Collections.Generic;
using System.Reflection;

namespace Exo.Service;

/// <summary>Provides a generic cache keyed per assembly name.</summary>
/// <remarks>
/// <para>
/// This allows services to store information about assemblies they already parsed once.
/// By combining this with the list of available assemblies, it is possible to quickly know what types are available for loading.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements in the cache.</typeparam>
public interface IAssemblyParsedDataCache<T>
{
	/// <summary>Enumerates all entries in the cache.</summary>
	/// <remarks>This will only list entries for assemblies part of the <see cref="IAssemblyLoader.AvailableAssemblies"/> list.</remarks>
	/// <returns>An enumerable list of cache entries.</returns>
	IEnumerable<KeyValuePair<AssemblyName, T>> EnumerateAll();

	/// <summary>Tries to get the cached value for a specific assembly name.</summary>
	/// <param name="assemblyName">Assembly name for which a cache value is requested.</param>
	/// <param name="value">The cached value, or default value if not found.</param>
	/// <returns><see langword="true"/> if there was a cached value associated with the specified assembly name; otherwise <see langword="false"/>.</returns>
	bool TryGetValue(AssemblyName assemblyName, out T? value);

	/// <summary>Sets the cached value for a specific assembly name.</summary>
	/// <remarks>The assembly name must be part of the <see cref="IAssemblyLoader.AvailableAssemblies"/> list.</remarks>
	/// <param name="assemblyName">sembly name for which the cache value is to be defined.</param>
	/// <param name="value">The value to store in the cache.</param>
	/// <exception cref="ArgumentOutOfRangeException">The assembly name is not part of the available assemblies.</exception>
	void SetValue(AssemblyName assemblyName, T value);
}
