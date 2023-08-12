using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Exo.Ui.Contracts;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceViewModel : DeviceViewModel
{
	public LightingViewModel LightingViewModel { get; }
	public LightingZoneViewModel? UnifiedLightingZone { get; }
	public ReadOnlyCollection<LightingZoneViewModel> LightingZones { get; }

	private readonly Dictionary<Guid, LightingZoneViewModel> _lightingZoneById;

	private int _changedZoneCount;
	private int _busyZoneCount;
	private bool _useUnifiedLighting;

	public bool IsNotBusy => _busyZoneCount == 0;

	public bool IsChanged => _changedZoneCount != 0;

	public bool UseUnifiedLighting
	{
		get => _useUnifiedLighting;
		set
		{
			if (value == UnifiedLightingZone is null) throw new InvalidOperationException(value ? "This device does not support unified lighting." : "This device only supports unified lighting.");
			SetValue(ref _useUnifiedLighting, value);
		}
	}

	public LightingDeviceViewModel(LightingViewModel lightingViewModel, LightingDeviceInformation lightingDeviceInformation) : base(lightingDeviceInformation.DeviceInformation)
	{
		LightingViewModel = lightingViewModel;
		UnifiedLightingZone = lightingDeviceInformation.UnifiedLightingZone is not null ? new LightingZoneViewModel(this, lightingDeviceInformation.UnifiedLightingZone) : null;
		LightingZones = lightingDeviceInformation.LightingZones.IsDefaultOrEmpty ?
			ReadOnlyCollection<LightingZoneViewModel>.Empty :
			Array.AsReadOnly(Array.ConvertAll(lightingDeviceInformation.LightingZones.AsMutable(), z => new LightingZoneViewModel(this, z)));
		_lightingZoneById = new();
		if (UnifiedLightingZone is not null)
		{
			_lightingZoneById.Add(UnifiedLightingZone.Id, UnifiedLightingZone);
			UnifiedLightingZone.PropertyChanged += OnLightingZonePropertyChanged;
		}
		foreach (var zone in LightingZones)
		{
			_lightingZoneById[zone.Id] = zone;
			zone.PropertyChanged += OnLightingZonePropertyChanged;
		}
		_useUnifiedLighting = lightingDeviceInformation.LightingZones.IsDefaultOrEmpty;
	}

	private void OnLightingZonePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(LightingZoneViewModel.IsChanged))
		{
			if ((((LightingZoneViewModel)sender!).IsChanged ? _changedZoneCount++ : --_changedZoneCount) == 0)
			{
				NotifyPropertyChanged(ChangedProperty.IsChanged);
			}
		}
		else if (e.PropertyName == nameof(LightingZoneViewModel.IsNotBusy))
		{
			if ((((LightingZoneViewModel)sender!).IsNotBusy ? --_busyZoneCount : _busyZoneCount++) == 0)
			{
				NotifyPropertyChanged(ChangedProperty.IsNotBusy);
			}
		}
	}

	public async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (!IsNotBusy) throw new InvalidOperationException("The device is already busy applying changes.");

		var zoneEffects = ImmutableArray.CreateBuilder<ZoneLightEffect>();
		try
		{
			if (UseUnifiedLighting && UnifiedLightingZone is not null)
			{
				if (UnifiedLightingZone.BuildEffect() is { } effect)
				{
					UnifiedLightingZone.OnBeforeApplyingChanges();
					zoneEffects.Add(new() { ZoneId = UnifiedLightingZone.Id, Effect = effect });
				}
			}
			else
			{
				foreach (var zone in LightingZones)
				{
					if (zone.IsChanged)
					{
						if (zone.BuildEffect() is { } effect)
						{
							zone.OnBeforeApplyingChanges();
							zoneEffects.Add(new() { ZoneId = zone.Id, Effect = effect });
						}
					}
				}
			}
			if (zoneEffects.Count > 0)
			{
				await LightingViewModel.LightingService.ApplyDeviceLightingEffectsAsync(new() { DeviceId = DeviceId, ZoneEffects = zoneEffects.DrainToImmutable() }, cancellationToken);
			}
		}
		catch
		{
			foreach (var zone in LightingZones)
			{
				zone.OnAfterApplyingChangesCancellation();
			}
		}
	}

	public void Reset()
	{
		if (UseUnifiedLighting && UnifiedLightingZone is not null)
		{
			if (UnifiedLightingZone.IsChanged)
			{
				UnifiedLightingZone.Reset();
			}
		}
		else
		{
			foreach (var zone in LightingZones)
			{
				if (zone.IsChanged)
				{
					zone.Reset();
				}
			}
		}
	}

	public LightingZoneViewModel GetLightingZone(Guid zoneId) => _lightingZoneById[zoneId];

	public LightingEffect? GetActiveLightingEffect(Guid zoneId) => LightingViewModel.GetActiveLightingEffect(DeviceId, zoneId);
}