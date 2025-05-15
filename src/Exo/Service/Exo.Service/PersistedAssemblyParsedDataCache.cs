using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Exo.Configuration;

namespace Exo.Service;

public static class PersistedAssemblyParsedDataCache
{
	public static async Task<PersistedAssemblyParsedDataCache<T>> CreateAsync<T>(IAssemblyLoader assemblyLoader, IConfigurationContainer<AssemblyName> assemblyConfigurationService, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
		where T : notnull
	{
		var cache = new ConcurrentDictionary<string, T>();

		foreach (var assembly in assemblyLoader.AvailableAssemblies)
		{
			var result = await assemblyConfigurationService.ReadValueAsync<T>(assembly, jsonTypeInfo, cancellationToken).ConfigureAwait(false);

			if (result.Found)
			{
				cache.TryAdd(assembly.FullName, result.Value!);
			}
		}

		return new PersistedAssemblyParsedDataCache<T>(cache, assemblyLoader, assemblyConfigurationService, jsonTypeInfo);
	}
}

public sealed class PersistedAssemblyParsedDataCache<T> : IAssemblyParsedDataCache<T>, IAsyncDisposable
	where T : notnull
{
	private readonly ConcurrentDictionary<string, T> _cache;
	private readonly IAssemblyLoader _assemblyLoader;
	private readonly IConfigurationContainer<AssemblyName> _assemblyConfigurationService;
	private readonly JsonTypeInfo<T> _jsonTypeInfo;
	private readonly ChannelWriter<KeyValuePair<AssemblyName, T>> _changeWriter;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _persistenceTask;

	internal PersistedAssemblyParsedDataCache(ConcurrentDictionary<string, T> cache, IAssemblyLoader assemblyLoader, IConfigurationContainer<AssemblyName> assemblyConfigurationService, JsonTypeInfo<T> jsonTypeInfo)
	{
		_cache = cache;
		_assemblyLoader = assemblyLoader;
		_assemblyConfigurationService = assemblyConfigurationService;
		_jsonTypeInfo = jsonTypeInfo;
		var channel = Channel.CreateUnbounded<KeyValuePair<AssemblyName, T>>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });
		_changeWriter = channel;
		_cancellationTokenSource = new();
		_persistenceTask = PersistAsync(channel, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

		_changeWriter.TryComplete();
		// Allow some time for pending writes to complete.
		cts.CancelAfter(10_000);
		await _persistenceTask.ConfigureAwait(false);
		cts.Dispose();
	}

	private async Task PersistAsync(ChannelReader<KeyValuePair<AssemblyName, T>> reader, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var kvp in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					await _assemblyConfigurationService.WriteValueAsync<T>(kvp.Key, kvp.Value, _jsonTypeInfo, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	public IEnumerable<KeyValuePair<AssemblyName, T>> EnumerateAll()
	{
		foreach (var assemblyName in _assemblyLoader.AvailableAssemblies)
		{
			if (_cache.TryGetValue(assemblyName.FullName, out var value))
			{
				yield return new KeyValuePair<AssemblyName, T>(assemblyName, value);
			}
		}
	}

	public bool TryGetValue(AssemblyName assemblyName, out T? value)
		=> _cache.TryGetValue(assemblyName.FullName, out value);

	public void SetValue(AssemblyName assemblyName, T value)
	{
		_cache[assemblyName.FullName] = value;
		_changeWriter.TryWrite(new(assemblyName, value));
	}
}
