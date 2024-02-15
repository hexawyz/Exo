using System.Reflection;
using System.Runtime.Loader;

namespace Exo.Service;

/// <summary>An assembly load context for the various plugins assemblies.</summary>
/// <remarks>
/// This is taken from the docs on how to do plugin loading, with slight upgrades.
/// We will always allow assembly unloading, because the intent is to avoid keeping useless stuff in memory.
/// i.e. You unplug a device, its driver is unloaded, the assembly is potentially unloaded.
/// </remarks>
public sealed class PluginLoadContext : AssemblyLoadContext
{
	private readonly AssemblyDependencyResolver _resolver;
	private readonly AssemblyName _mainAssemblyName;
	private readonly IAssemblyLoader _assemblyLoader;

	public PluginLoadContext(IAssemblyLoader assemblyLoader, AssemblyName mainAssemblyName, string pluginPath)
		: base(true)
	{
		_assemblyLoader = assemblyLoader;
		_mainAssemblyName = mainAssemblyName;
		_resolver = new AssemblyDependencyResolver(pluginPath);
	}

	protected override Assembly? Load(AssemblyName assemblyName)
	{
		if (assemblyName.FullName != _mainAssemblyName.FullName && _assemblyLoader.TryLoadAssembly(assemblyName) is { } assembly)
		{
			return assembly;
		}
		if (_resolver.ResolveAssemblyToPath(assemblyName) is string assemblyPath)
		{
			return LoadFromAssemblyPath(assemblyPath);
		}
		return null;
	}

	protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
	{
		if (_resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is string libraryPath)
		{
			return LoadUnmanagedDllFromPath(libraryPath);
		}

		return IntPtr.Zero;
	}
}
