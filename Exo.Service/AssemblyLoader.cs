using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading.Channels;

namespace Exo.Service;

internal sealed class AssemblyLoader : IAssemblyLoader, IMetadataSourceProvider, IDisposable
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
	private readonly HashSet<AssemblyCacheEntry> _loadedAssemblies = new();
	private ChannelWriter<(WatchNotificationKind Kind, string AssemblyPath, MetadataArchiveCategories AvailableMetadataArchives)>[]? _metadataChangeListeners;

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
			var assemblyNameDictionary = new Dictionary<string, (AssemblyName AssemblyName, string Path)>(assemblyPaths.Length);

			for (int i = 0; i < assemblyPaths.Length; i++)
			{
				var path = assemblyPaths[i];
				var assemblyName = AssemblyLoadContext.GetAssemblyName(path);
				assemblyNames[i] = assemblyName;
				assemblyNameDictionary.Add(assemblyName.FullName, (assemblyName, path));
			}

			foreach (var assemblyName in _availableAssemblies)
			{
				if (!assemblyNameDictionary.Remove(assemblyName.FullName))
				{
					if (_availableAssemblyDetails.TryRemove(assemblyName.FullName, out var entry))
					{
						entry.Dispose();
						if (_loadedAssemblies.Remove(entry))
						{
							_metadataChangeListeners.TryWrite((WatchNotificationKind.Removal, entry.Path, entry.AvailableMetadataArchives));
						}
					}
				}
			}

			foreach (var kvp in assemblyNameDictionary)
			{
				if (_availableAssemblyDetails.ContainsKey(kvp.Key)) continue;

				_availableAssemblyDetails.TryAdd(kvp.Key, new AssemblyCacheEntry(kvp.Value.AssemblyName, kvp.Value.Path, DetectAvailableMetadataArchives(kvp.Value.Path)));
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
			if (assembly is not null) return assembly;

			var context = new PluginLoadContext(this, entry.AssemblyName, entry.Path);
			context.Unloading += OnContextUnloading;
			_logger.AssemblyLoadContextCreated(context.Name!);
			entry.SetContext(context);
			assembly = context.MainAssembly;
		}

		lock (_updateLock)
		{
			_loadedAssemblies.Add(entry);
			_metadataChangeListeners.TryWrite((WatchNotificationKind.Addition, entry.Path, entry.AvailableMetadataArchives));
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
		if (_availableAssemblyDetails.TryGetValue(obj.Name!, out var entry))
		{
			lock (_updateLock)
			{
				if (_loadedAssemblies.Remove(entry))
				{
					_metadataChangeListeners.TryWrite((WatchNotificationKind.Removal, entry.Path, entry.AvailableMetadataArchives));
				}
			}
		}
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

	private static string GetCategorySuffix(MetadataArchiveCategory category)
		=> category switch
		{
			MetadataArchiveCategory.Strings => ".Strings.xoa",
			MetadataArchiveCategory.LightingEffects => ".LightingEffects.xoa",
			MetadataArchiveCategory.LightingZones => ".LightingZones.xoa",
			MetadataArchiveCategory.Sensors => ".Sensors.xoa",
			MetadataArchiveCategory.Coolers => ".Coolers.xoa",
			_ => throw new InvalidOperationException(),
		};

	private static MetadataSourceChangeNotification CreateNotification(WatchNotificationKind kind, string assemblyPath, MetadataArchiveCategory category, int extensionLength)
		=> new(kind, category, $"{assemblyPath.AsSpan(0, assemblyPath.Length - extensionLength)}{GetCategorySuffix(category)}");

	public async IAsyncEnumerable<MetadataSourceChangeNotification> WatchMetadataSourceChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<(WatchNotificationKind Kind, string AssemblyPath, MetadataArchiveCategories AvailableMetadataArchives)>();

		AssemblyCacheEntry[] loadedAssemblies;
		lock (_updateLock)
		{
			loadedAssemblies = [.. _loadedAssemblies];
			ArrayExtensions.InterlockedAdd(ref _metadataChangeListeners, channel);
		}

		try
		{
			foreach (var entry in loadedAssemblies)
			{
				if (entry.AvailableMetadataArchives == 0) continue;

				int extensionLength = Path.GetExtension(entry.Path.AsSpan()).Length;

				if ((entry.AvailableMetadataArchives & MetadataArchiveCategories.Strings) != 0) yield return CreateNotification(WatchNotificationKind.Enumeration, entry.Path, MetadataArchiveCategory.Strings, extensionLength);
				if ((entry.AvailableMetadataArchives & MetadataArchiveCategories.LightingEffects) != 0) yield return CreateNotification(WatchNotificationKind.Enumeration, entry.Path, MetadataArchiveCategory.LightingEffects, extensionLength);
				if ((entry.AvailableMetadataArchives & MetadataArchiveCategories.LightingZones) != 0) yield return CreateNotification(WatchNotificationKind.Enumeration, entry.Path, MetadataArchiveCategory.LightingZones, extensionLength);
				if ((entry.AvailableMetadataArchives & MetadataArchiveCategories.Sensors) != 0) yield return CreateNotification(WatchNotificationKind.Enumeration, entry.Path, MetadataArchiveCategory.Sensors, extensionLength);
				if ((entry.AvailableMetadataArchives & MetadataArchiveCategories.Coolers) != 0) yield return CreateNotification(WatchNotificationKind.Enumeration, entry.Path, MetadataArchiveCategory.Coolers, extensionLength);
			}

			await foreach (var (kind, assemblyPath, availableMetadataArchives) in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				int extensionLength = Path.GetExtension(assemblyPath.AsSpan()).Length;

				if ((availableMetadataArchives & MetadataArchiveCategories.Strings) != 0) yield return CreateNotification(kind, assemblyPath, MetadataArchiveCategory.Strings, extensionLength);
				if ((availableMetadataArchives & MetadataArchiveCategories.LightingEffects) != 0) yield return CreateNotification(kind, assemblyPath, MetadataArchiveCategory.LightingEffects, extensionLength);
				if ((availableMetadataArchives & MetadataArchiveCategories.LightingZones) != 0) yield return CreateNotification(kind, assemblyPath, MetadataArchiveCategory.LightingZones, extensionLength);
				if ((availableMetadataArchives & MetadataArchiveCategories.Sensors) != 0) yield return CreateNotification(kind, assemblyPath, MetadataArchiveCategory.Sensors, extensionLength);
				if ((availableMetadataArchives & MetadataArchiveCategories.Coolers) != 0) yield return CreateNotification(kind, assemblyPath, MetadataArchiveCategory.Coolers, extensionLength);
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _metadataChangeListeners, channel);
		}
	}
}
