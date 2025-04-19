using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Contracts;
using Exo.Lighting;
using Exo.Primitives;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal sealed class LightingEffectMetadataService : IChangeSource<LightingEffectInformation>, IAsyncDisposable
{
	[TypeId(0x3B7410BA, 0xF28E, 0x498E, 0xB7, 0x23, 0x4A, 0xE9, 0x09, 0xDF, 0xBA, 0xFC)]
	public readonly struct PersistedLightingEffectInformation
	{
		public required ImmutableArray<ConfigurablePropertyInformation> Properties { get; init; }
	}

	public static async ValueTask<LightingEffectMetadataService> CreateAsync
	(
		ILogger<LightingEffectMetadataService> logger,
		IConfigurationContainer<Guid> lightingEffectConfigurationContainer,
		CancellationToken cancellationToken
	)
	{
		var effectMetadataCache = new Dictionary<Guid, LightingEffectInformation>();

		var effectIds = await lightingEffectConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		foreach (var effectId in effectIds)
		{
			var result = await lightingEffectConfigurationContainer.ReadValueAsync<PersistedLightingEffectInformation>(effectId, cancellationToken).ConfigureAwait(false);
			if (!result.Found)
			{
				// TODO: Log
				continue;
			}
			var effectInformation = result.Value;
			effectMetadataCache.TryAdd(effectId, new() { EffectId = effectId, Properties = effectInformation.Properties });
		}

		return new(logger, lightingEffectConfigurationContainer, effectMetadataCache);
	}

	private readonly Dictionary<Guid, LightingEffectInformation> _effectMetadataCache;
	private readonly Lock _effectUpdateLock;
	private ChangeBroadcaster<LightingEffectInformation> _effectChangeBroadcaster;
	private readonly IConfigurationContainer<Guid> _lightingEffectConfigurationContainer;
	private readonly ILogger<LightingEffectMetadataService> _logger;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _runTask;

	public LightingEffectMetadataService
	(
		ILogger<LightingEffectMetadataService> logger,
		IConfigurationContainer<Guid> lightingEffectConfigurationContainer,
		Dictionary<Guid, LightingEffectInformation> effectMetadataCache
	)
	{
		_effectMetadataCache = effectMetadataCache;
		_effectUpdateLock = new();
		_logger = logger;
		_lightingEffectConfigurationContainer = lightingEffectConfigurationContainer;
		_cancellationTokenSource = new();
		_runTask = RunAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _runTask.ConfigureAwait(false);
			cts.Dispose();
		}
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var metadata in EffectSerializer.WatchEffectsAsync(cancellationToken).ConfigureAwait(false))
			{
				bool isNewOrChanged;
				lock (_effectUpdateLock)
				{
					isNewOrChanged = !_effectMetadataCache.TryGetValue(metadata.EffectId, out var oldMetadata) || metadata != oldMetadata;
					if (isNewOrChanged)
					{
						_effectMetadataCache[metadata.EffectId] = metadata;
						_effectChangeBroadcaster.Push(metadata);
					}
				}
				if (isNewOrChanged)
				{
					try
					{
						await PersistEffectInformationAsync(metadata, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						// TODO: Log
					}
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	ValueTask<LightingEffectInformation[]?> IChangeSource<LightingEffectInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<LightingEffectInformation> writer, CancellationToken cancellationToken)
	{
		lock (_effectUpdateLock)
		{
			LightingEffectInformation[] initialEffects = [.. _effectMetadataCache.Values];
			_effectChangeBroadcaster.Register(writer);
			return new(initialEffects);
		}
	}

	void IChangeSource<LightingEffectInformation>.UnregisterWatcher(ChannelWriter<LightingEffectInformation> writer)
	{
		_effectChangeBroadcaster.Unregister(writer);
	}

	private ValueTask PersistEffectInformationAsync(LightingEffectInformation info, CancellationToken cancellationToken)
		=> _lightingEffectConfigurationContainer.WriteValueAsync
		(
			info.EffectId,
			new PersistedLightingEffectInformation() { Properties = info.Properties },
			cancellationToken
		);
}
