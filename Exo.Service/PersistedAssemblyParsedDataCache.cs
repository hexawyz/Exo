using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

namespace Exo.Service;

public static class PersistedAssemblyParsedDataCache
{
	public static async Task<PersistedAssemblyParsedDataCache<T>> CreateAsync<T>(IAssemblyLoader assemblyLoader, ConfigurationService configurationService, CancellationToken cancellationToken)
	{
		var cache = new ConcurrentDictionary<string, T>();

		foreach (var assembly in assemblyLoader.AvailableAssemblies)
		{
			var result = await configurationService.ReadAssemblyConfigurationAsync<T>(assembly, cancellationToken).ConfigureAwait(false);

			if (result.Found)
			{
				cache.TryAdd(assembly.FullName, result.Value!);
			}
		}

		return new PersistedAssemblyParsedDataCache<T>(cache, assemblyLoader, configurationService);
	}
}

public sealed class PersistedAssemblyParsedDataCache<T> : IAssemblyParsedDataCache<T>, IAsyncDisposable
{
	private readonly ConcurrentDictionary<string, T> _cache;
	private readonly IAssemblyLoader _assemblyLoader;
	private readonly ConfigurationService _configurationService;
	private readonly ChannelWriter<KeyValuePair<AssemblyName, T>> _changeWriter;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _persistanceTask;

	internal PersistedAssemblyParsedDataCache(ConcurrentDictionary<string, T> cache, IAssemblyLoader assemblyLoader, ConfigurationService configurationService)
	{
		_cache = cache;
		_assemblyLoader = assemblyLoader;
		_configurationService = configurationService;
		var channel = Channel.CreateUnbounded<KeyValuePair<AssemblyName, T>>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });
		_changeWriter = channel;
		_cancellationTokenSource = new();
		_persistanceTask = PersistAsync(channel, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

		_changeWriter.TryComplete();
		// Allow some time for pending writes to complete.
		cts.CancelAfter(10_000);
		await _persistanceTask.ConfigureAwait(false);
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
					await _configurationService.WriteAssemblyConfigurationAsync<T>(kvp.Key, kvp.Value, cancellationToken).ConfigureAwait(false);
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
