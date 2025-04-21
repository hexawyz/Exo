using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Exo.Service;

internal sealed class DynamicAssemblyDiscovery : IAssemblyDiscovery
{
	// TODO: Implement
	public event EventHandler? AssemblyPathsChanged { add { } remove { } }

	public ImmutableArray<string> AssemblyPaths { get; }

	public DynamicAssemblyDiscovery(string basePath)
	{
		var assembly = typeof(DynamicAssemblyDiscovery).Assembly;

		var pluginDirectory = Path.GetFullPath(Path.Combine(basePath, "plugins"));

		AssemblyPaths = ImmutableCollectionsMarshal.AsImmutableArray(Directory.GetFiles(pluginDirectory, "*.dll"));
	}
}
