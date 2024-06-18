using System.Reflection;
using System.Runtime.CompilerServices;
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
	private object? _externalAssemblyReferences;

	public PluginLoadContext(IAssemblyLoader assemblyLoader, AssemblyName mainAssemblyName, string pluginPath)
		: base(mainAssemblyName.FullName, true)
	{
		_assemblyLoader = assemblyLoader;
		_mainAssemblyName = mainAssemblyName;
		_resolver = new AssemblyDependencyResolver(pluginPath);
	}

	protected override Assembly? Load(AssemblyName assemblyName)
	{
		if (assemblyName.FullName != _mainAssemblyName.FullName && _assemblyLoader.TryLoadAssembly(assemblyName) is { } assemblyReference)
		{
			AddReference(ref _externalAssemblyReferences, assemblyReference);
			return assemblyReference.Assembly;
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

	private static void AddReference(ref object? storage, LoadedAssemblyReference reference)
	{
		object newValue = reference;
		object? oldValue = null;

		while (oldValue != (oldValue = Interlocked.CompareExchange(ref storage, reference, null)))
		{
			if (oldValue is null) continue;

			if (oldValue is LoadedAssemblyReference)
			{
				newValue = (LoadedAssemblyReference[])[Unsafe.As<LoadedAssemblyReference>(oldValue), reference];
			}
			else
			{
				newValue = (LoadedAssemblyReference[])[.. Unsafe.As<LoadedAssemblyReference[]>(oldValue), reference];
			}
		}
	}
}
