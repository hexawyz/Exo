using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingViewModel : BindableObject
{
	private static readonly Dictionary<Guid, string> HardcodedGuidNames = new()
	{
		{ new Guid(0x34D2462C, 0xE510, 0x4A44, 0xA7, 0x0E, 0x14, 0x91, 0x32, 0x87, 0x25, 0xF9), "Z490 Motherboard Lighting" },
		{ new Guid(0xD57413D5, 0x5EA2, 0x49DD, 0xA5, 0x0A, 0x25, 0x83, 0xBB, 0x1B, 0xCA, 0x2A), "IO Shield" },
		{ new Guid(0x7D5C9B9F, 0x96A0, 0x472B, 0xA3, 0x4E, 0xFB, 0x10, 0xA8, 0x40, 0x74, 0x22), "PCH" },
		{ new Guid(0xB4913C2D, 0xEF7F, 0x49A0, 0x8A, 0xE6, 0xB3, 0x39, 0x2F, 0xD0, 0x9F, 0xA1), "PCI" },
		{ new Guid(0xBEC225CD, 0x72F7, 0x43E6, 0xB7, 0xC2, 0x2D, 0xB3, 0x6F, 0x09, 0xF2, 0xAA), "LED 1" },
		{ new Guid(0x1D012FD6, 0xA097, 0x4EA8, 0xB0, 0x2C, 0xBD, 0x31, 0xB4, 0xB4, 0xC9, 0xC6), "LED 2" },
		{ new Guid(0x435444B9, 0x2EA9, 0x4F2B, 0x85, 0xDA, 0xC3, 0xDA, 0x05, 0x21, 0x66, 0xE5), "Addressable LED 1" },
		{ new Guid(0xDB94A671, 0xB844, 0x4002, 0xA0, 0x96, 0x47, 0x4E, 0x9D, 0x1E, 0x4A, 0x49), "Addressable LED 2" },
	};

	internal ILightingService LightingService { get; }
	private readonly ObservableCollection<LightingDeviceViewModel> _lightingDevices;
	private readonly ConcurrentDictionary<string, LightingEffectViewModel> _effectViewModelCache;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public ObservableCollection<LightingDeviceViewModel> LightingDevices => _lightingDevices;

	public LightingViewModel(ILightingService lightingService)
	{
		LightingService = lightingService;
		_lightingDevices = new();
		_effectViewModelCache = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in LightingService.WatchLightingDevicesAsync(cancellationToken))
			{
				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Arrival:
					await CacheEffectInformationAsync(notification, cancellationToken).ConfigureAwait(false);
					_lightingDevices.Add(new(this, notification.Details));
					break;
				case WatchNotificationKind.Removal:
					for (int i = 0; i < _lightingDevices.Count; i++)
					{
						if (_lightingDevices[i].UniqueId == notification.Details.DeviceInformation.UniqueId)
						{
							_lightingDevices.RemoveAt(i);
							break;
						}
					}
					break;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task CacheEffectInformationAsync(WatchNotification<LightingDeviceInformation> notification, CancellationToken cancellationToken)
	{
		if (notification.Details.UnifiedLightingZone is { } unifiedZone) await CacheEffectInformationAsync(unifiedZone.SupportedEffectTypeNames, cancellationToken);
		foreach (var zone in notification.Details.LightingZones)
		{
			await CacheEffectInformationAsync(zone.SupportedEffectTypeNames, cancellationToken).ConfigureAwait(false);
		}
	}

	private async ValueTask CacheEffectInformationAsync(ImmutableArray<string> effectTypeNames, CancellationToken cancellationToken)
		=> await Parallel.ForEachAsync(effectTypeNames.AsMutable(), cancellationToken, CacheEffectInformationAsync).ConfigureAwait(false);

	private async ValueTask CacheEffectInformationAsync(string effectTypeName, CancellationToken cancellationToken)
	{
		if (!_effectViewModelCache.ContainsKey(effectTypeName) &&
			await LightingService.GetEffectInformationAsync(new EffectTypeReference { TypeName = effectTypeName }, cancellationToken).ConfigureAwait(false) is { } effectInformation)
		{
			_effectViewModelCache.TryAdd(effectTypeName, new(effectInformation));
		}
	}

	public LightingEffectViewModel GetEffect(string effectTypeName)
		=> _effectViewModelCache.TryGetValue(effectTypeName, out var effect) ? effect : throw new InvalidOperationException("Missing effect information.");

	public string GetZoneName(Guid zoneId) => HardcodedGuidNames.TryGetValue(zoneId, out string? zoneName) ? zoneName : $"Unknown {zoneId:B}";
}
