using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Exo.Contracts;
using Exo.Settings.Ui.Services;
using Exo.Ui;
using Exo.Contracts.Ui.Settings;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingViewModel : BindableObject, IAsyncDisposable
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

		{ new Guid(0x22C9ECBE, 0xD047, 0x4E26, 0xB0, 0xF4, 0x73, 0x0E, 0x5B, 0x3E, 0x40, 0x7E), "GPU Top 0" },
		{ new Guid(0x95A687EF, 0x6CBB, 0x4C22, 0xAA, 0xDB, 0x79, 0x18, 0x67, 0x44, 0xAD, 0xDA), "GPU Front 0" },
		{ new Guid(0x93505342, 0x3BEE, 0x4C22, 0xA5, 0x1E, 0xDD, 0x29, 0xCC, 0xF6, 0x55, 0xED), "GPU Back 0" },
		{ new Guid(0x211DA55C, 0xDFCC, 0x4A4D, 0xA9, 0x4E, 0x7F, 0x3C, 0xFA, 0x00, 0xC7, 0x22), "SLI Top 0" },

		{ new(0xB2BED1D4, 0x81AE, 0x48AE, 0x9A, 0x05, 0x09, 0xE5, 0x5C, 0x77, 0xAE, 0xBA), "Memory Module 1" },
		{ new(0xCB8A8ACC, 0x94DA, 0x4D7F, 0x89, 0xE4, 0x7A, 0x3C, 0xA4, 0x2C, 0x30, 0x50), "Memory Module 2" },
		{ new(0x0D4237BB, 0x02CC, 0x4BA3, 0xB8, 0x48, 0x67, 0x19, 0x80, 0xB1, 0xB9, 0xF8), "Memory Module 3" },
		{ new(0x3A782612, 0xF492, 0x4817, 0xA9, 0xD1, 0x85, 0x2A, 0xD9, 0x62, 0x8B, 0x09), "Memory Module 4" },
		{ new(0x477EACEE, 0xBDE8, 0x45D3, 0x9E, 0x39, 0x81, 0xDA, 0x03, 0x52, 0x36, 0x72), "Memory Module 5" },
		{ new(0xD2CC4954, 0x868F, 0x4F9F, 0xAD, 0x11, 0x94, 0xEE, 0x25, 0xD2, 0x15, 0xA9), "Memory Module 6" },
		{ new(0xBDE005C1, 0x8BC6, 0x4126, 0xBE, 0xF0, 0xB8, 0x84, 0x02, 0x71, 0xD6, 0xD5), "Memory Module 7" },
		{ new(0xA3BBB8F5, 0x16E6, 0x4AF2, 0x9B, 0x46, 0x9C, 0xEC, 0x68, 0xC2, 0x4E, 0x95), "Memory Module 8" },
	};

	internal ILightingService LightingService { get; }
	private readonly DevicesViewModel _devicesViewModel;
	private readonly ObservableCollection<LightingDeviceViewModel> _lightingDevices;
	private readonly Dictionary<Guid, LightingDeviceViewModel> _lightingDeviceById;
	private readonly ConcurrentDictionary<Guid, LightingEffectViewModel> _effectViewModelById;
	private readonly Dictionary<(Guid, Guid), LightingEffect> _activeLightingEffects;
	private readonly Dictionary<Guid, byte> _brightnessLevels;
	private readonly Dictionary<Guid, LightingDeviceInformation> _pendingDeviceInformations;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchDevicesTask;
	private readonly Task _watchEffectsTask;
	private readonly Task _watchBrightnessTask;

	public ObservableCollection<LightingDeviceViewModel> LightingDevices => _lightingDevices;

	public LightingViewModel(ILightingService lightingService, DevicesViewModel devicesViewModel, IEditionService editionService)
	{
		_devicesViewModel = devicesViewModel;
		LightingService = lightingService;
		_lightingDevices = new();
		_lightingDeviceById = new();
		_effectViewModelById = new();
		_activeLightingEffects = new();
		_brightnessLevels = new();
		_pendingDeviceInformations = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_watchDevicesTask = WatchDevicesAsync(_cancellationTokenSource.Token);
		_watchEffectsTask = WatchEffectsAsync(_cancellationTokenSource.Token);
		_watchBrightnessTask = WatchBrightnessAsync(_cancellationTokenSource.Token);
		_devicesViewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchDevicesTask.ConfigureAwait(false);
		await _watchEffectsTask.ConfigureAwait(false);
		await _watchBrightnessTask.ConfigureAwait(false);
	}

	private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			var vm = (DeviceViewModel)e.NewItems![0]!;
			if (_pendingDeviceInformations.Remove(vm.Id, out var info))
			{
				OnDeviceAdded(vm, info);
			}
		}
		else if (e.Action == NotifyCollectionChangedAction.Remove)
		{
			var vm = (DeviceViewModel)e.OldItems![0]!;
			if (!_pendingDeviceInformations.Remove(vm.Id))
			{
				OnDeviceRemoved(vm.Id);
			}
		}
		else
		{
			// As of writing this code, we don't require support for anything else, but if this change in the future, this exception will be triggered.
			throw new InvalidOperationException("This case is not handled.");
		}
	}

	private void OnDeviceAdded(DeviceViewModel device, LightingDeviceInformation lightingDeviceInformation)
	{
		var vm = new LightingDeviceViewModel(this, device, lightingDeviceInformation);
		_lightingDevices.Add(vm);
		_lightingDeviceById[vm.Id] = vm;
	}

	private void OnDeviceRemoved(Guid deviceId)
	{
		for (int i = 0; i < _lightingDevices.Count; i++)
		{
			var vm = _lightingDevices[i];
			if (_lightingDevices[i].Id == deviceId)
			{
				_lightingDevices.RemoveAt(i);
				_lightingDeviceById.Remove(vm.Id);
				break;
			}
		}
	}

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var info in LightingService.WatchLightingDevicesAsync(cancellationToken))
			{
				if (_lightingDeviceById.TryGetValue(info.DeviceId, out var vm))
				{
					// TODO: Update lighting zones ?
				}
				else
				{
					try
					{
						await CacheEffectInformationAsync(info, cancellationToken);
					}
					catch
					{
					}

					if (_devicesViewModel.TryGetDevice(info.DeviceId, out var device))
					{
						OnDeviceAdded(device, info);
					}
					else if (!_devicesViewModel.IsRemovedId(info.DeviceId))
					{
						_pendingDeviceInformations.Add(info.DeviceId, info);
					}
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

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchBrightnessAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in LightingService.WatchBrightnessAsync(cancellationToken))
			{
				_brightnessLevels[notification.DeviceId] = notification.BrightnessLevel;
				if (_lightingDeviceById.TryGetValue(notification.DeviceId, out var vm))
				{
					vm.OnBrightnessUpdated();
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task CacheEffectInformationAsync(LightingDeviceInformation information, CancellationToken cancellationToken)
	{
		if (information.UnifiedLightingZone is { } unifiedZone) await CacheEffectInformationAsync(unifiedZone.SupportedEffectIds, cancellationToken);
		if (!information.LightingZones.IsDefaultOrEmpty)
		{
			foreach (var zone in information.LightingZones)
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

	public byte? GetBrightness(Guid deviceId)
		=> _brightnessLevels.TryGetValue(deviceId, out var brightness) ? brightness : null;
}
