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
	// TODO: Migrate to external files.
	private static readonly Dictionary<Guid, string> HardcodedGuidNames = new()
	{
		{ new Guid(0x7105A4FA, 0x2235, 0x49FC, 0xA7, 0x5A, 0xFD, 0x0D, 0xEC, 0x13, 0x51, 0x99), "LG Monitor" },

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
	private readonly Dictionary<Guid, LightingDeviceViewModel> _lightingDeviceById;
	private readonly ConcurrentDictionary<Guid, LightingEffectViewModel> _effectViewModelById;
	private readonly Dictionary<(Guid, Guid), LightingEffect> _activeLightingEffects;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchDevicesTask;
	private readonly Task _watchEffectsTask;

	public ObservableCollection<LightingDeviceViewModel> LightingDevices => _lightingDevices;

	public LightingViewModel(ILightingService lightingService)
	{
		LightingService = lightingService;
		_lightingDevices = new();
		_lightingDeviceById = new();
		_effectViewModelById = new();
		_activeLightingEffects = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_watchDevicesTask = WatchDevicesAsync(_cancellationTokenSource.Token);
		_watchEffectsTask = WatchEffectsAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchDevicesTask.ConfigureAwait(false);
		await _watchEffectsTask.ConfigureAwait(false);
	}

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in LightingService.WatchLightingDevicesAsync(cancellationToken))
			{
				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Arrival:
					{
						await CacheEffectInformationAsync(notification, cancellationToken);
						var vm = new LightingDeviceViewModel(this, notification.Details);
						_lightingDevices.Add(vm);
						_lightingDeviceById[vm.DeviceId] = vm;
					}
					break;
				case WatchNotificationKind.Removal:
					for (int i = 0; i < _lightingDevices.Count; i++)
					{
						var vm = _lightingDevices[i];
						if (_lightingDevices[i].DeviceId == notification.Details.DeviceInformation.DeviceId)
						{
							_lightingDevices.RemoveAt(i);
							_lightingDeviceById.Remove(vm.DeviceId);
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

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchEffectsAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in LightingService.WatchEffectsAsync(cancellationToken))
			{
				if (notification.Effect is not null)
				{
					_activeLightingEffects[(notification.DeviceId, notification.ZoneId)] = notification.Effect;
				}
				else
				{
					_activeLightingEffects.Remove((notification.DeviceId, notification.ZoneId));
				}
				// We need the effect to be cached before any view model accesses it.
				if (notification.Effect is not null) await CacheEffectInformationAsync(notification.Effect.EffectId, cancellationToken);
				if (_lightingDeviceById.TryGetValue(notification.DeviceId, out var vm))
				{
					vm.GetLightingZone(notification.ZoneId).OnEffectUpdated();
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task CacheEffectInformationAsync(WatchNotification<LightingDeviceInformation> notification, CancellationToken cancellationToken)
	{
		if (notification.Details.UnifiedLightingZone is { } unifiedZone) await CacheEffectInformationAsync(unifiedZone.SupportedEffectIds, cancellationToken);
		if (!notification.Details.LightingZones.IsDefaultOrEmpty)
		{
			foreach (var zone in notification.Details.LightingZones)
			{
				await CacheEffectInformationAsync(zone.SupportedEffectIds, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async ValueTask CacheEffectInformationAsync(ImmutableArray<Guid> effectIds, CancellationToken cancellationToken)
		=> await Parallel.ForEachAsync(effectIds.AsMutable(), cancellationToken, CacheEffectInformationAsync).ConfigureAwait(false);

	private async ValueTask CacheEffectInformationAsync(Guid effectId, CancellationToken cancellationToken)
	{
		if (!_effectViewModelById.ContainsKey(effectId) &&
			await LightingService.GetEffectInformationAsync(new EffectTypeReference { TypeId = effectId }, cancellationToken).ConfigureAwait(false) is { } effectInformation)
		{
			_effectViewModelById.TryAdd(effectId, new(effectInformation));
		}
	}

	public LightingEffectViewModel GetEffect(Guid effectId)
		=> _effectViewModelById.TryGetValue(effectId, out var effect) ? effect : throw new InvalidOperationException("Missing effect information.");

	public string GetZoneName(Guid zoneId) => HardcodedGuidNames.TryGetValue(zoneId, out string? zoneName) ? zoneName : $"Unknown {zoneId:B}";

	public LightingEffect? GetActiveLightingEffect(Guid deviceId, Guid zoneId)
		=> _activeLightingEffects.TryGetValue((deviceId, zoneId), out var effect) ? effect : null;
}