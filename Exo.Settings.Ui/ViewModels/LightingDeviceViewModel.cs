using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.Contracts;
using Exo.Contracts.Ui.Settings;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceViewModel : BindableObject
{
	private readonly DeviceViewModel _deviceViewModel;

	public LightingViewModel LightingViewModel { get; }
	public LightingZoneViewModel? UnifiedLightingZone { get; }
	public ReadOnlyCollection<LightingZoneViewModel> LightingZones { get; }
	public LightingDeviceBrightnessCapabilitiesViewModel? BrightnessCapabilities { get; }
	public LightingDeviceBrightnessViewModel? Brightness { get; }

	private readonly Dictionary<Guid, LightingZoneViewModel> _lightingZoneById;

	private int _changedZoneCount;
	private int _busyZoneCount;
	private bool _useUnifiedLighting;

	private readonly Commands.ApplyChangesCommand _applyChangesCommand;
	private readonly Commands.ResetChangesCommand _resetChangesCommand;

	public bool IsNotBusy => _busyZoneCount == 0;

	public bool IsChanged => AreZonesChanged || IsBrightnessChanged;

	private bool AreZonesChanged => _changedZoneCount != 0;
	private bool IsBrightnessChanged => Brightness?.IsChanged == true;

	public bool UseUnifiedLighting
	{
		get => _useUnifiedLighting;
		set
		{
			if (value == UnifiedLightingZone is null) throw new InvalidOperationException(value ? "This device does not support unified lighting." : "This device only supports unified lighting.");
			SetValue(ref _useUnifiedLighting, value);
		}
	}

	public Guid Id => _deviceViewModel.Id;

	public string FriendlyName => _deviceViewModel.FriendlyName;
	public DeviceCategory Category => _deviceViewModel.Category;
	public bool IsAvailable => _deviceViewModel.IsAvailable;

	public ICommand ApplyChangesCommand => _applyChangesCommand;
	public ICommand ResetChangesCommand => _resetChangesCommand;

	public LightingDeviceViewModel(LightingViewModel lightingViewModel, DeviceViewModel deviceViewModel, LightingDeviceInformation lightingDeviceInformation)
	{
		_deviceViewModel = deviceViewModel;
		LightingViewModel = lightingViewModel;
		_applyChangesCommand = new(this);
		_resetChangesCommand = new(this);
		UnifiedLightingZone = lightingDeviceInformation.UnifiedLightingZone is not null ? new LightingZoneViewModel(this, lightingDeviceInformation.UnifiedLightingZone) : null;
		LightingZones = lightingDeviceInformation.LightingZones.IsDefaultOrEmpty ?
			ReadOnlyCollection<LightingZoneViewModel>.Empty :
			Array.AsReadOnly(Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(lightingDeviceInformation.LightingZones)!, z => new LightingZoneViewModel(this, z)));
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
		if (lightingDeviceInformation.BrightnessCapabilities is { } brightnessCapabilities)
		{
			BrightnessCapabilities = new(brightnessCapabilities);
			Brightness = new(brightnessCapabilities);
			Brightness.PropertyChanged += OnBrightnessPropertyChanged;
		}
		OnBrightnessUpdated();
		_deviceViewModel.PropertyChanged += OnDevicePropertyChanged;
	}

	private static bool CompareProperty(PropertyChangedEventArgs a, PropertyChangedEventArgs b)
		=> a == b || a.PropertyName == b.PropertyName;

	private void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (CompareProperty(e, ChangedProperty.FriendlyName) || CompareProperty(e, ChangedProperty.Category) || CompareProperty(e, ChangedProperty.IsAvailable))
		{
			NotifyPropertyChanged(e);
		}
	}

	private void OnBrightnessPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(LightingZoneViewModel.IsChanged))
		{
			if (!AreZonesChanged)
			{
				OnChanged();
			}
		}
	}

	private void OnLightingZonePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(LightingZoneViewModel.IsChanged))
		{
			if ((((LightingZoneViewModel)sender!).IsChanged ? _changedZoneCount++ : --_changedZoneCount) == 0 && !IsBrightnessChanged)
			{
				OnChanged();
			}
		}
		else if (e.PropertyName == nameof(LightingZoneViewModel.IsNotBusy))
		{
			if ((((LightingZoneViewModel)sender!).IsNotBusy ? --_busyZoneCount : _busyZoneCount++) == 0 && !IsBrightnessChanged)
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
				var lightingService = await LightingViewModel.ConnectionManager.GetLightingServiceAsync(cancellationToken);
				await lightingService.ApplyDeviceLightingChangesAsync
				(
					new()
					{
						DeviceId = _deviceViewModel.Id,
						BrightnessLevel = Brightness?.Level ?? 0,
						ZoneEffects = zoneEffects.DrainToImmutable()
					},
					cancellationToken
				);
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

	private void Reset()
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

	private void OnChanged()
	{
		_applyChangesCommand.OnChanged();
		_resetChangesCommand.OnChanged();
		NotifyPropertyChanged(ChangedProperty.IsChanged);
	}

	public LightingZoneViewModel GetLightingZone(Guid zoneId) => _lightingZoneById[zoneId];

	public LightingEffect? GetActiveLightingEffect(Guid zoneId) => LightingViewModel.GetActiveLightingEffect(_deviceViewModel.Id, zoneId);

	public void OnBrightnessUpdated()
	{
		if (Brightness is { } vm && LightingViewModel.GetBrightness(_deviceViewModel.Id) is byte brightnessLevel)
		{
			vm.SetInitialBrightness(brightnessLevel);
		}
	}

	private static class Commands
	{
		public sealed class ApplyChangesCommand : ICommand
		{
			private readonly LightingDeviceViewModel _owner;

			public ApplyChangesCommand(LightingDeviceViewModel owner) => _owner = owner;

			public bool CanExecute(object? parameter) => _owner.IsChanged;
			public async void Execute(object? parameter) => await _owner.ApplyChangesAsync(default);

			public event EventHandler? CanExecuteChanged;

			internal void OnChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		public sealed class ResetChangesCommand : ICommand
		{
			private readonly LightingDeviceViewModel _owner;

			public ResetChangesCommand(LightingDeviceViewModel owner) => _owner = owner;

			public bool CanExecute(object? parameter) => _owner.IsChanged;
			public void Execute(object? parameter) => _owner.Reset();

			public event EventHandler? CanExecuteChanged;

			internal void OnChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
