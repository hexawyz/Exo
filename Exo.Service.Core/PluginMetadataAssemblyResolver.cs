using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Exo.Service;

public sealed class PluginMetadataAssemblyResolver : MetadataAssemblyResolver
{
	private readonly AssemblyDependencyResolver _resolver;
	private readonly string _appDirectory;
	private readonly string _runtimeDirectory;

	public PluginMetadataAssemblyResolver(string pluginPath)
	{
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

		if (assemblyName.Name == typeof(object).Assembly.GetName().Name)
		{
			return context.LoadFromAssemblyPath(typeof(object).Assembly.Location);
		}

		return TryLoadFrom(context, _appDirectory, assemblyName) ?? TryLoadFrom(context, _runtimeDirectory, assemblyName);
	}

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
