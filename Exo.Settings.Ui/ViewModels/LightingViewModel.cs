using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.InteropServices;
using Exo.Contracts;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingViewModel : BindableObject, IConnectedState, IAsyncDisposable
{
	internal SettingsServiceConnectionManager ConnectionManager { get; }
	private readonly DevicesViewModel _devicesViewModel;
	private readonly ISettingsMetadataService _metadataService;
	private readonly ObservableCollection<LightingDeviceViewModel> _lightingDevices;
	private readonly Dictionary<Guid, LightingDeviceViewModel> _lightingDeviceById;
	private readonly ConcurrentDictionary<Guid, LightingEffectViewModel> _effectViewModelById;
	private readonly Dictionary<(Guid, Guid), LightingEffect> _activeLightingEffects;
	private readonly Dictionary<Guid, byte> _brightnessLevels;
	private readonly Dictionary<Guid, LightingDeviceInformation> _pendingDeviceInformations;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public ObservableCollection<LightingDeviceViewModel> LightingDevices => _lightingDevices;

	public LightingViewModel(SettingsServiceConnectionManager connectionManager, DevicesViewModel devicesViewModel, ISettingsMetadataService metadataService)
	{
		_devicesViewModel = devicesViewModel;
		_metadataService = metadataService;
		ConnectionManager = connectionManager;
		_lightingDevices = new();
		_lightingDeviceById = new();
		_effectViewModelById = new();
		_activeLightingEffects = new();
		_brightnessLevels = new();
		_pendingDeviceInformations = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_devicesViewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
		_stateRegistration = ConnectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		_stateRegistration.Dispose();
		return ValueTask.CompletedTask;
	}

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource.IsCancellationRequested) return;
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			await _metadataService.WaitForAvailabilityAsync(cancellationToken);

			var watchDevicesTask = WatchDevicesAsync(cts.Token);
			var watchEffectsTask = WatchEffectsAsync(cts.Token);
			var watchBrightnessTask = WatchBrightnessAsync(cts.Token);

			try
			{
				await Task.WhenAll([watchDevicesTask, watchEffectsTask, watchBrightnessTask]);
			}
			catch
			{
			}
		}
	}

	void IConnectedState.Reset()
	{
		_lightingDeviceById.Clear();
		_effectViewModelById.Clear();
		_activeLightingEffects.Clear();
		_brightnessLevels.Clear();
		_pendingDeviceInformations.Clear();

		foreach (var device in _lightingDevices)
		{
			device.Dispose();
		}

		_lightingDevices.Clear();
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
		else if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			// Reset will only be triggered when the service connection is reset. In that case, the change will be handled in the appropriate reset code for this component.
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
			var lightingService = await ConnectionManager.GetLightingServiceAsync(cancellationToken);
			await foreach (var info in lightingService.WatchLightingDevicesAsync(cancellationToken))
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
			var lightingService = await ConnectionManager.GetLightingServiceAsync(cancellationToken);
			await foreach (var notification in lightingService.WatchEffectsAsync(cancellationToken))
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
			var lightingService = await ConnectionManager.GetLightingServiceAsync(cancellationToken).ConfigureAwait(false);
			await foreach (var notification in lightingService.WatchBrightnessAsync(cancellationToken))
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
				await CacheEffectInformationAsync(zone.SupportedEffectIds, cancellationToken);
			}
		}
	}

	private async ValueTask CacheEffectInformationAsync(ImmutableArray<Guid> effectIds, CancellationToken cancellationToken)
		=> await Parallel.ForEachAsync(ImmutableCollectionsMarshal.AsArray(effectIds)!, cancellationToken, CacheEffectInformationAsync);

	private async ValueTask CacheEffectInformationAsync(Guid effectId, CancellationToken cancellationToken)
	{
		var lightingService = await ConnectionManager.GetLightingServiceAsync(cancellationToken);
		if (!_effectViewModelById.ContainsKey(effectId) &&
			await lightingService.GetEffectInformationAsync(new EffectTypeReference { TypeId = effectId }, cancellationToken) is { } effectInformation)
		{
			string? displayName = null;
			if (_metadataService.TryGetLightingEffectMetadata("", "", effectInformation.EffectId, out var metadata))
			{
				displayName = _metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
			}
			displayName ??= string.Create(CultureInfo.InvariantCulture, $"Effect {effectInformation.EffectId:B}.");

			_effectViewModelById.TryAdd(effectId, new(effectInformation, displayName));
		}
	}

	public LightingEffectViewModel GetEffect(Guid effectId)
		=> _effectViewModelById.TryGetValue(effectId, out var effect) ? effect : throw new InvalidOperationException("Missing effect information.");

	public string GetZoneName(Guid zoneId)
	{
		string? displayName = null;
		if (_metadataService.TryGetLightingZoneMetadata("", "", zoneId, out var metadata))
		{
			displayName = _metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
		}
		return displayName ?? $"Unknown {zoneId:B}";
	}

	public LightingEffect? GetActiveLightingEffect(Guid deviceId, Guid zoneId)
		=> _activeLightingEffects.TryGetValue((deviceId, zoneId), out var effect) ? effect : null;

	public byte? GetBrightness(Guid deviceId)
		=> _brightnessLevels.TryGetValue(deviceId, out var brightness) ? brightness : null;
}
