using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

public sealed class PluginMetadataAssemblyResolver : MetadataAssemblyResolver
{
	private readonly Func<AssemblyName, string?> _assemblyLocator;
	private readonly AssemblyDependencyResolver _resolver;
	private readonly string _appDirectory;
	private readonly string _runtimeDirectory;

	public PluginMetadataAssemblyResolver(AssemblyName mainAssemblyName, Func<AssemblyName, string?> assemblyLocator)
	{
		_assemblyLocator = assemblyLocator;
		string pluginPath = assemblyLocator(mainAssemblyName) ?? throw new FileNotFoundException($"Could not locate assembly {mainAssemblyName}.");
		_resolver = new AssemblyDependencyResolver(pluginPath);
		_appDirectory = Path.GetDirectoryName(typeof(PluginMetadataAssemblyResolver).Assembly.Location)!;
		_runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
	}

	public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
	{
		if (_resolver.ResolveAssemblyToPath(assemblyName) is string path)
		{
			return context.LoadFromAssemblyPath(path);
		}

		// TODO: Check using AppDomain.CurrentDomain.GetAssemblies() instead ?
		if (assemblyName.Name == typeof(object).Assembly.GetName().Name)
		{
			return context.LoadFromAssemblyPath(typeof(object).Assembly.Location);
		}
		else if (assemblyName.Name == typeof(ILogger).Assembly.GetName().Name)
		{
			return context.LoadFromAssemblyPath(typeof(ILogger).Assembly.Location);
		}

		return TryLoadFrom(context, _appDirectory, assemblyName) ?? TryLoadFrom(context, _runtimeDirectory, assemblyName) ?? TryLoadFromLocator(context, _assemblyLocator, assemblyName);
	}

	private static Assembly? TryLoadFromLocator(MetadataLoadContext context, Func<AssemblyName, string?> assemblyLocator, AssemblyName assemblyName)
		=> assemblyLocator(assemblyName) is string path && File.Exists(path) ?
			context.LoadFromAssemblyPath(path) :
			null;

	private static Assembly? TryLoadFrom(MetadataLoadContext context, string directory, AssemblyName assemblyName)
	{
		string path = Path.GetFullPath(Path.Combine(directory, assemblyName.Name + ".dll"));
		if (File.Exists(path))
		{
			return context.LoadFromAssemblyPath(path);
		}

		return null;
	}
}
