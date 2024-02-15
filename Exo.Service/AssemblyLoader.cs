using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Exo.Service;

internal sealed class AssemblyLoader : IAssemblyLoader, IDisposable
{
	private sealed class AssemblyCacheEntry
	{
		public AssemblyCacheEntry(AssemblyName assemblyName, string path)
		{
			AssemblyName = assemblyName;
			Path = path;
			Lock = new();
			WeakReference = new(null!);
		}

		public AssemblyName AssemblyName { get; }
		public string Path { get; }
		public object Lock { get; }
		public WeakReference<Assembly> WeakReference { get; }
	}

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
					_availableAssemblyDetails.TryRemove(assemblyName.FullName, out _);
				}
			}

			foreach (var kvp in assemblyNameDictionary)
			{
				_availableAssemblyDetails.TryAdd(kvp.Key.FullName, new AssemblyCacheEntry(kvp.Key, kvp.Value));
			}

			Volatile.Write(ref _availableAssemblies, assemblyNames);

			AvailableAssembliesChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public Assembly LoadAssembly(AssemblyName assemblyName)
	{
		var entry = GetAssemblyCacheEntry(assemblyName);

		return entry.WeakReference.TryGetTarget(out var assembly)
			? assembly
			: LoadAssemblySlow(entry);
	}

	public Assembly? TryLoadAssembly(AssemblyName assemblyName)
	{
		if (!_availableAssemblyDetails.TryGetValue(assemblyName.FullName, out var entry)) return null;

		return entry.WeakReference.TryGetTarget(out var assembly)
			? assembly
			: LoadAssemblySlow(entry);
	}

	private Assembly LoadAssemblySlow(AssemblyCacheEntry entry)
	{
		Assembly? assembly;

		lock (entry.Lock)
		{
			if (!entry.WeakReference.TryGetTarget(out assembly))
			{
				var context = new PluginLoadContext(this, entry.AssemblyName, entry.Path);
				assembly = context.LoadFromAssemblyName(entry.AssemblyName);
				entry.WeakReference.SetTarget(assembly);
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
