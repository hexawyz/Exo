using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Contracts;
using Exo.Primitives;

namespace Exo.Service;

internal sealed class AssemblyLoader : IAssemblyLoader, IDisposable
{
	// Used to persist the name of assemblies that were loaded at least once.
	// Normally, the component discovery and loading system should strictly avoid unnecessarily loading assemblies,
	// so all assemblies listed here will have been loaded once for a valid reason.
	// This information will be used to feed the UI with a list of metadata that should be loaded for proper function.
	[TypeId(0x4DD1E437, 0x1394, 0x45A4, 0xAA, 0x33, 0x3F, 0x1C, 0x45, 0xD2, 0x39, 0xC1)]
	private readonly struct UsedAssemblyDetails
	{
		public readonly ImmutableArray<string> UsedAssemblyNames { get; init; }
	}

	private sealed class AssemblyCacheEntry
	{
		public AssemblyCacheEntry(AssemblyName assemblyName, string path, MetadataArchiveCategories availableMetadataArchives)
		{
			AssemblyName = assemblyName;
			Path = path;
			AvailableMetadataArchives = availableMetadataArchives;
			Lock = new();
		}

		public AssemblyName AssemblyName { get; }
		public string Path { get; }
		public object Lock { get; }
		private WeakReference<LoadedAssemblyReference>? _assemblyReference;
		public MetadataArchiveCategories AvailableMetadataArchives { get; }

		public LoadedAssemblyReference? TryGetAssemblyReference() => Volatile.Read(ref _assemblyReference) is { } assemblyReference && assemblyReference.TryGetTarget(out var reference) ?
			reference :
			null;

		public LoadedAssemblyReference SetContext(PluginLoadContext context)
		{
			var reference = new LoadedAssemblyReference(context, context.LoadFromAssemblyName(AssemblyName));
			if (_assemblyReference is null)
			{
				_assemblyReference = new(reference, false);
			}
			else
			{
				_assemblyReference.SetTarget(reference);
			}
			return reference;
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

	public static async Task<AssemblyLoader> CreateAsync
	(
		ILogger<AssemblyLoader> logger,
		IConfigurationContainer configurationContainer,
		IAssemblyDiscovery assemblyDiscovery,
		string mainAssemblyPath,
		CancellationToken cancellationToken
	)
	{
		var result = await configurationContainer.ReadValueAsync<UsedAssemblyDetails>(cancellationToken).ConfigureAwait(false);
		var assembliesUsedAtLeastOnce = result.Found ?
			result.Value.UsedAssemblyNames.NotNull() :
			[];

		return new(logger, configurationContainer, assemblyDiscovery, assembliesUsedAtLeastOnce, mainAssemblyPath);
	}

	private readonly ILogger<AssemblyLoader> _logger;
	private readonly IAssemblyDiscovery _assemblyDiscovery;
	private readonly ConcurrentDictionary<string, AssemblyCacheEntry> _availableAssemblyDetails = new(1, 20);
	private AssemblyName[] _availableAssemblies = [];
	private readonly object _updateLock = new();
	private readonly HashSet<AssemblyCacheEntry> _loadedAssemblies = new();
	private ChangeBroadcaster<AssemblyChangeNotification> _assemblyChangeBroadcaster;
	private readonly HashSet<string> _assembliesUsedAtLeastOnce;
	private readonly IConfigurationContainer _configurationContainer;
	private readonly Timer _configurationUpdateTimer;
	private bool _isConfigurationUpdateScheduled;

	public event EventHandler<AssemblyLoadEventArgs>? AfterAssemblyLoad;
	public event EventHandler? AvailableAssembliesChanged;

	private readonly Action<AssemblyLoadContext> _onContextUnloading;

	private readonly string _mainAssemblyPath;

	public ImmutableArray<AssemblyName> AvailableAssemblies => Unsafe.As<AssemblyName[], ImmutableArray<AssemblyName>>(ref _availableAssemblies);

	private AssemblyLoader(ILogger<AssemblyLoader> logger, IConfigurationContainer configurationContainer, IAssemblyDiscovery assemblyDiscovery, ImmutableArray<string> assembliesUsedAtLeastOnce, string mainAssemblyPath)
	{
		_logger = logger;
		_mainAssemblyPath = mainAssemblyPath;
		_assemblyDiscovery = assemblyDiscovery;
		_configurationContainer = configurationContainer;
		_assembliesUsedAtLeastOnce = assembliesUsedAtLeastOnce.ToHashSet();
		_configurationUpdateTimer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
		_assemblyDiscovery.AssemblyPathsChanged += OnAvailableAssembliesChanged;
		_onContextUnloading = OnContextUnloading;
		OnAvailableAssembliesChanged(_assemblyDiscovery, EventArgs.Empty);
	}

	public void Dispose()
	{
		_assemblyDiscovery.AssemblyPathsChanged -= OnAvailableAssembliesChanged;
	}

	private void OnTimerTick(object? state)
	{
		lock (_updateLock)
		{
			PersistUsedAssemblies(new() { UsedAssemblyNames = _assembliesUsedAtLeastOnce.ToImmutableArray() });
			_isConfigurationUpdateScheduled = false;
		}
	}

	// ⚠️ To be called within the update lock.
	private void UnsafeScheduleConfigurationUpdate()
	{
		if (!_isConfigurationUpdateScheduled)
		{
			_configurationUpdateTimer.Change(0, Timeout.Infinite);
			_isConfigurationUpdateScheduled = true;
		}
	}

	private async void PersistUsedAssemblies(UsedAssemblyDetails details)
	{
		try
		{
			await PersistUsedAssembliesAsync(details, default).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	private ValueTask PersistUsedAssembliesAsync(UsedAssemblyDetails details, CancellationToken cancellationToken)
		=> _configurationContainer.WriteValueAsync(details, cancellationToken);

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
						if (_loadedAssemblies.Remove(entry))
						{
							if (_assembliesUsedAtLeastOnce.Remove(entry.AssemblyName.FullName)) UnsafeScheduleConfigurationUpdate();
							_assemblyChangeBroadcaster.Push(new(WatchNotificationKind.Removal, entry.Path, entry.AvailableMetadataArchives));
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

	public LoadedAssemblyReference LoadAssembly(AssemblyName assemblyName)
	{
		var entry = GetAssemblyCacheEntry(assemblyName);

		return entry.TryGetAssemblyReference() is { } reference ?
			reference :
			LoadAssemblySlow(entry);
	}

	public LoadedAssemblyReference? TryLoadAssembly(AssemblyName assemblyName)
	{
		if (!_availableAssemblyDetails.TryGetValue(assemblyName.FullName, out var entry)) return null;

		return entry.TryGetAssemblyReference() is { } reference ?
			reference :
			LoadAssemblySlow(entry);
	}

	private LoadedAssemblyReference LoadAssemblySlow(AssemblyCacheEntry entry)
	{
		LoadedAssemblyReference? reference;

		lock (entry.Lock)
		{
			reference = entry.TryGetAssemblyReference();
			if (reference is not null) return reference;

			var context = new PluginLoadContext(this, entry.AssemblyName, entry.Path);
			context.Unloading += _onContextUnloading;
			_logger.AssemblyLoadContextCreated(context.Name!);
			reference = entry.SetContext(context);
		}

		lock (_updateLock)
		{
			_loadedAssemblies.Add(entry);
			if (_assembliesUsedAtLeastOnce.Add(entry.AssemblyName.FullName))
			{
				UnsafeScheduleConfigurationUpdate();
				_assemblyChangeBroadcaster.Push(new(WatchNotificationKind.Addition, entry.Path, entry.AvailableMetadataArchives));
			}
		}

		try
		{
			AfterAssemblyLoad?.Invoke(this, new AssemblyLoadEventArgs(reference));
		}
		catch (Exception ex)
		{
			_logger.AssemblyLoaderAfterAssemblyLoadError(entry.AssemblyName.FullName, ex);
		}

		return reference;
	}

	private void OnContextUnloading(AssemblyLoadContext obj)
	{
		_logger.AssemblyLoadContextUnloading(obj.Name!);
		if (_availableAssemblyDetails.TryGetValue(obj.Name!, out var entry))
		{
			lock (_updateLock)
			{
				// NB: We don't emit a metadata removal notification (anymore) here because metadata of unloaded assemblies is still needed from within the UI.
				_loadedAssemblies.Remove(entry);
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


	ValueTask<AssemblyChangeNotification[]?> IChangeSource<AssemblyChangeNotification>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<AssemblyChangeNotification> writer, CancellationToken cancellationToken)
	{
		AssemblyChangeNotification[] loadedAssemblies;
		lock (_updateLock)
		{
			loadedAssemblies = new AssemblyChangeNotification[_assembliesUsedAtLeastOnce.Count + 1];
			int i = 0;
			loadedAssemblies[i++] = new(WatchNotificationKind.Enumeration, _mainAssemblyPath, MetadataArchiveCategories.Strings | MetadataArchiveCategories.LightingEffects);
			foreach (var assemblyName in _assembliesUsedAtLeastOnce)
			{
				if (_availableAssemblyDetails.TryGetValue(assemblyName, out var details))
				{
					loadedAssemblies[i++] = new(WatchNotificationKind.Enumeration, details.Path, details.AvailableMetadataArchives);
				}
			}
			if (i < loadedAssemblies.Length) Array.Resize(ref loadedAssemblies, i);
			_assemblyChangeBroadcaster.Register(writer);
		}
		return new(loadedAssemblies);
	}

	void IChangeSource<AssemblyChangeNotification>.UnregisterWatcher(ChannelWriter<AssemblyChangeNotification> writer)
	{
		_assemblyChangeBroadcaster.Unregister(writer);
	}
}
