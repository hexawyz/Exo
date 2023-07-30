using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceViewModel : DeviceViewModel
{
	public LightingViewModel LightingViewModel { get; }
	public LightingZoneViewModel? UnifiedLightingZone { get; }
	public ReadOnlyCollection<LightingZoneViewModel> LightingZones { get; }

	private bool _isBusy;
	public bool IsNotBusy
	{
		get => _isBusy;
		private set => SetValue(ref _isBusy, !value);
	}

	private bool _isModified;
	public bool IsModified
	{
		get => _isModified;
		private set => SetValue(ref _isModified, value);
	}

	public LightingDeviceViewModel(LightingViewModel lightingViewModel, LightingDeviceInformation lightingDeviceInformation) : base(lightingDeviceInformation.DeviceInformation)
	{
		LightingViewModel = lightingViewModel;
		UnifiedLightingZone = lightingDeviceInformation.UnifiedLightingZone is not null ? new LightingZoneViewModel(this, lightingDeviceInformation.UnifiedLightingZone) : null;
		LightingZones = Array.AsReadOnly(Array.ConvertAll(lightingDeviceInformation.LightingZones, z => new LightingZoneViewModel(this, z)));
	}

	public void SetModified() => IsModified = true;

	public void UpdateModified()
	{
		bool isModified = false;
		foreach (var zone in LightingZones)
		{
			isModified |= zone.IsModified;
		}
	}

	public async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		IsNotBusy = false;
		var zoneEffects = ImmutableArray.CreateBuilder<ZoneLightEffect>();
		try
		{
			foreach (var zone in LightingZones)
			{
				if (zone.IsModified)
				{
					if (zone.BuildEffect() is { } effect)
					{
						zoneEffects.Add(new() { ZoneId = zone.Id, Effect = effect });
					}
				}
			}
			if (zoneEffects.Count > 0)
			{
				await LightingViewModel.LightingService.ApplyDeviceLightingEffectsAsync(new() { UniqueId = UniqueId, ZoneEffects = zoneEffects.DrainToImmutable() }, cancellationToken);
			}
		}
		catch
		{
			IsNotBusy = true;
			throw;
		}
	}
}
