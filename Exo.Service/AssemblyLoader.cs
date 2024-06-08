using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Exo.Service;

internal sealed class AssemblyLoader : IAssemblyLoader, IDisposable
{
	[Flags]
	private enum MetadataArchiveCategories
	{
		None = 0,
		Strings = 1,
		LightingEffects = 2,
		LightingZones = 4,
		Sensors = 8,
		Coolers = 16,
	}

	private sealed class AssemblyCacheEntry : IDisposable
	{
		public AssemblyCacheEntry(AssemblyName assemblyName, string path, MetadataArchiveCategories availableMetadataArchives)
		{
			AssemblyName = assemblyName;
			Path = path;
			AvailableMetadataArchives = availableMetadataArchives;
			Lock = new();
		}

		public void Dispose()
		{
			lock (Lock)
			{
				if (_dependentHandle.IsAllocated)
				{
					_dependentHandle.Dispose();
				}
			}
		}

		public AssemblyName AssemblyName { get; }
		public string Path { get; }
		public object Lock { get; }
		private DependentHandle _dependentHandle;
		public MetadataArchiveCategories AvailableMetadataArchives { get; }

		public Assembly? TryGetAssembly() => _dependentHandle.IsAllocated ? _dependentHandle.Target as Assembly : null;

		public void SetContext(PluginLoadContext context)
		{
			if (_dependentHandle.IsAllocated)
			{
				_dependentHandle.Target = context.MainAssembly;
				_dependentHandle.Dependent = context;
			}
			else
			{
				_dependentHandle = new(context.MainAssembly, context);
			}
		}
	}

	private static MetadataArchiveCategories DetectAvailableMetadataArchives(string path)
	{
		var pathWithoutExtension = path.AsMemory(0, path.Length - Path.GetExtension(path.AsSpan()).Length);

		MetadataArchiveCategories categories = 0;

		if (DoesFileExist(pathWithoutExtension, ".Strings.xoa")) categories |= MetadataArchiveCategories.Strings;
		if (DoesFileExist(pathWithoutExtension, ".LightingEffects.xoa")) categories |= MetadataArchiveCategories.LightingEffects;
		if (DoesFileExist(pathWithoutExtension, ".LightingZones.xoa")) categories |= MetadataArchiveCategories.LightingZones;
		if (DoesFileExist(pathWithoutExtension, ".Sensors.xoa")) categories |= MetadataArchiveCategories.Sensors;
		if (DoesFileExist(pathWithoutExtension, ".Coolers.xoa")) categories |= MetadataArchiveCategories.Coolers;

		return categories;
	}

	private static bool DoesFileExist(ReadOnlyMemory<char> pathWithoutExtension, string suffix)
		=> File.Exists($"{pathWithoutExtension.Span}{suffix}");

	private readonly ILogger<AssemblyLoader> _logger;
	private readonly IAssemblyDiscovery _assemblyDiscovery;
	private readonly ConcurrentDictionary<string, AssemblyCacheEntry> _availableAssemblyDetails = new(1, 20);
	private AssemblyName[] _availableAssemblies = [];
	private readonly object _updateLock = new();

	public event EventHandler<AssemblyLoadEventArgs>? AfterAssemblyLoad;
	public event EventHandler? AvailableAssembliesChanged;

	public ImmutableArray<AssemblyName> AvailableAssemblies => Unsafe.As<AssemblyName[], ImmutableArray<AssemblyName>>(ref _availableAssemblies);

	public AssemblyLoader(ILogger<AssemblyLoader> logger, IAssemblyDiscovery assemblyDiscovery)
	{
		_logger = logger;
		_assemblyDiscovery = assemblyDiscovery;
		_assemblyDiscovery.AssemblyPathsChanged += OnAvailableAssembliesChanged;
		OnAvailableAssembliesChanged(_assemblyDiscovery, EventArgs.Empty);
	}

	public void Dispose()
	{
		_assemblyDiscovery.AssemblyPathsChanged -= OnAvailableAssembliesChanged;
	}

	private void OnAvailableAssembliesChanged(object? sender, EventArgs e)
	{
		lock (_updateLock)
		{
			var assemblyPaths = _assemblyDiscovery.AssemblyPaths;
			var assemblyNames = new AssemblyName[assemblyPaths.Length];
			var assemblyNameDictionary = new Dictionary<AssemblyName, string>(assemblyPaths.Length);

			for (int i = 0; i < assemblyPaths.Length; i++)
			{
				var path = assemblyPaths[i];
				assemblyNameDictionary.Add(assemblyNames[i] = AssemblyLoadContext.GetAssemblyName(path), path);
			}

			foreach (var assemblyName in _availableAssemblies)
			{
				if (!assemblyNameDictionary.Remove(assemblyName))
				{
					if (_availableAssemblyDetails.TryRemove(assemblyName.FullName, out var entry))
					{
						entry.Dispose();
					}
				}
			}

			foreach (var kvp in assemblyNameDictionary)
			{
				_availableAssemblyDetails.TryAdd(kvp.Key.FullName, new AssemblyCacheEntry(kvp.Key, kvp.Value, DetectAvailableMetadataArchives(kvp.Value)));
			}

			Volatile.Write(ref _availableAssemblies, assemblyNames);

			AvailableAssembliesChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public Assembly LoadAssembly(AssemblyName assemblyName)
	{
		var entry = GetAssemblyCacheEntry(assemblyName);

		return entry.TryGetAssembly() is { } assembly ?
			assembly :
			LoadAssemblySlow(entry);
	}

	public Assembly? TryLoadAssembly(AssemblyName assemblyName)
	{
		if (!_availableAssemblyDetails.TryGetValue(assemblyName.FullName, out var entry)) return null;

		return entry.TryGetAssembly() is { } assembly ?
			assembly :
			LoadAssemblySlow(entry);
	}

	private Assembly LoadAssemblySlow(AssemblyCacheEntry entry)
	{
		Assembly? assembly;

		lock (entry.Lock)
		{
			assembly = entry.TryGetAssembly();
			if (assembly is null)
			{
				var context = new PluginLoadContext(this, entry.AssemblyName, entry.Path);
				context.Unloading += OnContextUnloading;
				_logger.AssemblyLoadContextCreated(context.Name!);
				entry.SetContext(context);
				assembly = context.MainAssembly;
			}
		}

		try
		{
			AfterAssemblyLoad?.Invoke(this, new AssemblyLoadEventArgs(assembly));
		}
		catch (Exception ex)
		{
			_logger.AssemblyLoaderAfterAssemblyLoadError(entry.AssemblyName.FullName, ex);
		}

		return assembly;
	}

	private void OnContextUnloading(AssemblyLoadContext obj)
	{
		_logger.AssemblyLoadContextUnloading(obj.Name!);
	}

	public MetadataLoadContext CreateMetadataLoadContext(AssemblyName assemblyName)
		=> new MetadataLoadContext
		(
			new PluginMetadataAssemblyResolver
			(
				GetAssemblyCacheEntry(assemblyName).AssemblyName,
				assemblyName => _availableAssemblyDetails.TryGetValue(assemblyName.FullName, out var entry) ? entry.Path : null
			),
			typeof(object).Assembly.GetName().Name
		);

	private AssemblyCacheEntry GetAssemblyCacheEntry(AssemblyName assemblyName)
	{
		if (!_availableAssemblyDetails.TryGetValue(assemblyName.FullName, out var entry))
		{
			throw new InvalidOperationException($"Only assemblies part of the {nameof(AvailableAssemblies)} list can be loaded.");
		}

		return entry;
	}
}
