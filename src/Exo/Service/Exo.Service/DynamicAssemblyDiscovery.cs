using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Exo.Service;

internal sealed class DynamicAssemblyDiscovery : IAssemblyDiscovery
{
	// TODO: Implement
	public event EventHandler? AssemblyPathsChanged { add { } remove { } }

	public ImmutableArray<string> AssemblyPaths { get; }

	public DynamicAssemblyDiscovery()
	{
		var assembly = typeof(DebugAssemblyDiscovery).Assembly;

		var assemblyDirectory = Path.GetDirectoryName(assembly.Location)!;
		var pluginDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, "plugins"));

		AssemblyPaths = ImmutableCollectionsMarshal.AsImmutableArray(Directory.GetFiles(pluginDirectory, "*.dll"));
	}
}
