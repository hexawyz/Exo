using System.Collections.Concurrent;
using System.Collections.Immutable;
using Exo.Configuration;
using Exo.Contracts;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal sealed class LightingEffectMetadataService : IDisposable
{
	[TypeId(0x3B7410BA, 0xF28E, 0x498E, 0xB7, 0x23, 0x4A, 0xE9, 0x09, 0xDF, 0xBA, 0xFC)]
	public readonly struct PersistedLightingEffectInformation
	{
		public required string TypeName { get; init; }
		public required ImmutableArray<ConfigurablePropertyInformation> Properties { get; init; }
	}

	public static async ValueTask<LightingEffectMetadataService> CreateAsync
	(
		ILogger<LightingEffectMetadataService> logger,
		IConfigurationContainer<Guid> lightingEffectConfigurationContainer,
		CancellationToken cancellationToken
	)
	{
		var effectMetadataCache = new ConcurrentDictionary<Guid, LightingEffectInformation>();

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
			effectMetadataCache.TryAdd(effectId, new() { EffectId = effectId, EffectTypeName = effectInformation.TypeName, Properties = effectInformation.Properties });
		}

		return new(logger, lightingEffectConfigurationContainer, effectMetadataCache);
	}

	private readonly ConcurrentDictionary<Guid, LightingEffectInformation> _effectMetadataCache;
	private readonly IConfigurationContainer<Guid> _lightingEffectConfigurationContainer;
	private readonly ILogger<LightingEffectMetadataService> _logger;
	private CancellationTokenSource? _cancellationTokenSource;

	public LightingEffectMetadataService
	(
		ILogger<LightingEffectMetadataService> logger,
		IConfigurationContainer<Guid> lightingEffectConfigurationContainer,
		ConcurrentDictionary<Guid, LightingEffectInformation> effectMetadataCache
	)
	{
		_logger = logger;
		_lightingEffectConfigurationContainer = lightingEffectConfigurationContainer;
		_effectMetadataCache = effectMetadataCache;
		_cancellationTokenSource = new();
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			cts.Dispose();
		}
	}

	// Retrieves the global cancellation token while checking that the instance is not disposed.
	// We use this cancellation token to cancel pending write operations.
	private CancellationToken GetCancellationToken()
	{
		var cts = Volatile.Read(ref _cancellationTokenSource);
		ObjectDisposedException.ThrowIf(cts is null, typeof(LightingEffectMetadataService));
		return cts.Token;
	}

	public void RegisterEffect(Type effectType)
	{
		var cancellationToken = GetCancellationToken();
		var info = EffectSerializer.GetEffectInformation(effectType);
		// NB: This is not the most efficient thing in the world, as we will always overwrite the current value, whether it has actually changed or not.
		// However, it might still be relatively negligible overall (considering how often this would actually be called) and we want to keep only one true reference.
		if (_effectMetadataCache.TryGetValue(info.EffectId, out var oldInfo))
		{
			if (ReferenceEquals(oldInfo, info)) return;
			_effectMetadataCache[info.EffectId] = info;
			// Do a full deep comparison of the objects to determine if we should update the persisted information. (We really want to minimize disk writes)
			if (info != oldInfo)
			{
				PersistEffectInformation(info, cancellationToken);
			}
		}
		else
		{
			_effectMetadataCache[info.EffectId] = info;
			PersistEffectInformation(info, cancellationToken);
		}
	}

	public LightingEffectInformation GetEffectInformation(Guid effectId)
	{
		var cancellationToken = GetCancellationToken();
		if (!_effectMetadataCache.TryGetValue(effectId, out var info))
		{
			info = EffectSerializer.GetEffectInformation(effectId);
			if (_effectMetadataCache.TryAdd(effectId, info))
			{
				PersistEffectInformation(info, cancellationToken);
			}
		}
		return info;
	}

	private async void PersistEffectInformation(LightingEffectInformation info, CancellationToken cancellationToken)
	{
		try
		{
			await PersistEffectInformationAsync(info, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	private ValueTask PersistEffectInformationAsync(LightingEffectInformation info, CancellationToken cancellationToken)
		=> _lightingEffectConfigurationContainer.WriteValueAsync
		(
			info.EffectId,
			new PersistedLightingEffectInformation() { TypeName = info.EffectTypeName, Properties = info.Properties },
			cancellationToken
		);
}
