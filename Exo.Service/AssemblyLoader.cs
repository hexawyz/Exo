using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using Microsoft.Extensions.Logging;

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
	private readonly ConcurrentDictionary<AssemblyName, AssemblyCacheEntry> _availableAssemblyDetails = new(1, 20);
	private AssemblyName[] _availableAssemblies = Array.Empty<AssemblyName>();
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
					_availableAssemblyDetails.TryRemove(assemblyName, out _);
				}
			}

			foreach (var kvp in assemblyNameDictionary)
			{
				_availableAssemblyDetails.TryAdd(kvp.Key, new AssemblyCacheEntry(kvp.Key, kvp.Value));
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

	private Assembly LoadAssemblySlow(AssemblyCacheEntry entry)
	{
		Assembly? assembly;

		lock (entry.Lock)
		{
			if (!entry.WeakReference.TryGetTarget(out assembly))
			{
				var context = new PluginLoadContext(entry.Path);
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

	public PEReader LoadForReflection(AssemblyName assemblyName)
	{
		var entry = GetAssemblyCacheEntry(assemblyName);

		var file = new FileStream(entry.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		try
		{
			return new PEReader(file, PEStreamOptions.Default);
		}
		catch
		{
			file.Dispose();
			throw;
		}
	}

	private AssemblyCacheEntry GetAssemblyCacheEntry(AssemblyName assemblyName)
	{
		if (!_availableAssemblyDetails.TryGetValue(assemblyName, out var entry))
		{
			throw new InvalidOperationException($"Only assemblies part of the {nameof(AvailableAssemblies)} list can be loaded.");
		}

		return entry;
	}
}
