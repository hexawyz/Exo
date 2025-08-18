using System.Threading.Channels;
using Exo.Configuration;
using Exo.Lighting;
using Exo.Primitives;
using Exo.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal sealed class LightingEffectMetadataService : IChangeSource<LightingEffectInformation>, IAsyncDisposable
{
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
			var result = await lightingEffectConfigurationContainer.ReadValueAsync(effectId, SourceGenerationContext.Default.PersistedLightingEffectInformation, cancellationToken).ConfigureAwait(false);
			if (!result.Found)
			{
				// TODO: Log
				continue;
			}
			var effectInformation = result.Value;
			effectMetadataCache.TryAdd(effectId, new() { EffectId = effectId, Capabilities = effectInformation.Capabilities, Properties = effectInformation.Properties });
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

	void IChangeSource<LightingEffectInformation>.UnsafeUnregisterWatcher(ChannelWriter<LightingEffectInformation> writer)
	{
		_effectChangeBroadcaster.Unregister(writer);
	}

	ValueTask IChangeSource<LightingEffectInformation>.SafeUnregisterWatcherAsync(ChannelWriter<LightingEffectInformation> writer)
	{
		lock (_effectUpdateLock)
		{
			_effectChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
		return ValueTask.CompletedTask;
	}

	private ValueTask PersistEffectInformationAsync(LightingEffectInformation info, CancellationToken cancellationToken)
		=> _lightingEffectConfigurationContainer.WriteValueAsync
		(
			info.EffectId,
			new PersistedLightingEffectInformation() { Capabilities = info.Capabilities, Properties = info.Properties },
			SourceGenerationContext.Default.PersistedLightingEffectInformation,
			cancellationToken
		);
}
