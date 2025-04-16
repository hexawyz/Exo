using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.Lighting;
using Exo.Service;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceViewModel : ChangeableBindableObject, IDisposable
{
	private readonly DeviceViewModel _deviceViewModel;

	public LightingViewModel LightingViewModel { get; }
	public LightingZoneViewModel? UnifiedLightingZone { get; }
	public ReadOnlyCollection<LightingZoneViewModel> LightingZones { get; }
	public LightingDeviceBrightnessCapabilitiesViewModel? BrightnessCapabilities { get; }
	public LightingDeviceBrightnessViewModel? Brightness { get; }

	private readonly Dictionary<Guid, LightingZoneViewModel> _lightingZoneById;

	private readonly LightingPersistenceMode _persistenceMode;

	private int _changedZoneCount;
	private int _busyZoneCount;
	private bool _useUnifiedLighting;
	private bool _useUnifiedLightingInitialValue;
	private bool _isExpanded;
	private bool _shouldPersistChanges;

	private readonly Commands.ApplyChangesCommand _applyChangesCommand;
	private readonly Commands.ResetChangesCommand _resetChangesCommand;

	public bool IsNotBusy => _busyZoneCount == 0;

	public override bool IsChanged => AreZonesChanged || IsBrightnessChanged || IsUseUnifiedLightingChanged || _shouldPersistChanges;

	private bool AreZonesChanged => _changedZoneCount != 0;
	private bool IsBrightnessChanged => Brightness?.IsChanged == true;
	private bool IsUseUnifiedLightingChanged => _useUnifiedLighting != _useUnifiedLightingInitialValue;

	public bool CanToggleUnifiedLighting => UnifiedLightingZone is not null && LightingZones.Count > 0;

	public bool UseUnifiedLighting
	{
		get => _useUnifiedLighting;
		set
		{
			bool wasChanged = IsChanged;
			if (value)
			{
				if (UnifiedLightingZone is null) throw new InvalidOperationException("This device does not support unified lighting.");
			}
			else
			{
				if (LightingZones.Count == 0) throw new InvalidOperationException("This device only supports unified lighting.");
			}
			SetValue(ref _useUnifiedLighting, value, ChangedProperty.UseUnifiedLighting);
			OnChangeStateChange(wasChanged);
		}
	}

	private bool IsUnifiedLightingInitiallyEnabled
	{
		get => _useUnifiedLightingInitialValue;
		set
		{
			bool wasChanged = IsChanged;
			if (_useUnifiedLighting == _useUnifiedLightingInitialValue)
			{
				_useUnifiedLighting = value;
			}
			_useUnifiedLightingInitialValue = value;
			OnChangeStateChange(wasChanged);
		}
	}

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	// This setting is purely local to the UI and will be reset everytime the apply button is pressed.
	// When the checkbox is checked, we consider the device to be changed, so that settings can be forcefully applied.
	public bool ShouldPersistChanges
	{
		get => _shouldPersistChanges;
		set
		{
			bool wasChanged = IsChanged;
			SetValue(ref _shouldPersistChanges, value);
			OnChangeStateChange(wasChanged);
		}
	}

	public bool CanPersistChanges => _persistenceMode == LightingPersistenceMode.CanPersist;

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
		LightingZoneViewModel CreateZoneViewModel(LightingZoneInformation lightingZone)
		{
			var (displayName, displayOrder) = LightingViewModel.GetZoneMetadata(lightingZone.ZoneId);
			return new LightingZoneViewModel(this, lightingZone, displayName, displayOrder);
		}
		LightingZoneViewModel[] CreateZoneViewModels(ImmutableArray<LightingZoneInformation> lightingZones)
		{
			var viewModels = Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(lightingZones)!, CreateZoneViewModel);
			Array.Sort
			(
				viewModels,
				static (a, b) =>
				{
					int r = Comparer<int>.Default.Compare(a.DisplayOrder, b.DisplayOrder);
					if (r == 0)
					{
						r = Comparer<Guid>.Default.Compare(a.Id, b.Id);
					}
					return r;
				}
			);
			return viewModels;
		}
		_persistenceMode = lightingDeviceInformation.PersistenceMode;
		UnifiedLightingZone = lightingDeviceInformation.UnifiedLightingZone is not null ? CreateZoneViewModel(lightingDeviceInformation.UnifiedLightingZone.GetValueOrDefault()) : null;
		LightingZones = lightingDeviceInformation.LightingZones.IsDefaultOrEmpty ?
			ReadOnlyCollection<LightingZoneViewModel>.Empty :
			Array.AsReadOnly(CreateZoneViewModels(lightingDeviceInformation.LightingZones));
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
		_useUnifiedLightingInitialValue = _useUnifiedLighting = lightingDeviceInformation.LightingZones.IsDefaultOrEmpty;
		if (lightingDeviceInformation.BrightnessCapabilities is { } brightnessCapabilities)
		{
			BrightnessCapabilities = new(brightnessCapabilities);
			Brightness = new(brightnessCapabilities);
			Brightness.PropertyChanged += OnBrightnessPropertyChanged;
		}
		_deviceViewModel.PropertyChanged += OnDevicePropertyChanged;
	}

	public void Dispose()
	{
		_deviceViewModel.PropertyChanged -= OnDevicePropertyChanged;
		if (UnifiedLightingZone is not null)
		{
			UnifiedLightingZone.PropertyChanged -= OnLightingZonePropertyChanged;
		}
		foreach (var zone in LightingZones)
		{
			zone.PropertyChanged -= OnLightingZonePropertyChanged;
		}
		if (Brightness is not null)
		{
			Brightness.PropertyChanged -= OnBrightnessPropertyChanged;
		}
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
		if (Equals(e, ChangedProperty.IsChanged))
		{
			bool isChanged = ((LightingDeviceBrightnessViewModel)sender!).IsChanged;

			if (isChanged != (AreZonesChanged || !IsUseUnifiedLightingChanged || !_shouldPersistChanges))
			{
				OnChanged(isChanged);
			}
		}
	}

	private void OnLightingZonePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.IsChanged))
		{
			bool isChanged = ((LightingZoneViewModel)sender!).IsChanged;
			if ((isChanged ? _changedZoneCount++ : --_changedZoneCount) == 0)
			{
				if (isChanged != (IsBrightnessChanged || IsUseUnifiedLightingChanged || _shouldPersistChanges))
				{
					OnChanged(isChanged);
				}
			}
		}
		else if (Equals(e, ChangedProperty.IsNotBusy))
		{
			bool isNotBusy = ((LightingZoneViewModel)sender!).IsNotBusy;
			if ((isNotBusy ? --_busyZoneCount : _busyZoneCount++) == 0)
			{
				NotifyPropertyChanged(ChangedProperty.IsNotBusy);
			}
		}
	}

	public async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (!IsNotBusy) throw new InvalidOperationException("The device is already busy applying changes.");

		var zoneEffects = ImmutableArray.CreateBuilder<LightingZoneEffect>();
		try
		{
			if (UseUnifiedLighting && UnifiedLightingZone is not null)
			{
				if (UnifiedLightingZone.BuildEffect() is { } effect)
				{
					UnifiedLightingZone.OnBeforeApplyingChanges();
					zoneEffects.Add(new(UnifiedLightingZone.Id, effect));
				}
			}
			else
			{
				foreach (var zone in LightingZones)
				{
					// If the unified lighting flag is changed, we forcefully update all (other) lighting zones.
					// Otherwise, we just update the lighting zones that changed.
					if (zone.IsChanged || IsUseUnifiedLightingChanged)
					{
						if (zone.BuildEffect() is { } effect)
						{
							zone.OnBeforeApplyingChanges();
							zoneEffects.Add(new(zone.Id, effect));
						}
					}
				}
			}
			if (zoneEffects.Count > 0 || ShouldPersistChanges)
			{
				if (LightingViewModel.LightingService is { } lightingService)
				{
					await lightingService.SetLightingAsync
					(
						new LightingDeviceConfigurationUpdate
						(
							_deviceViewModel.Id,
							ShouldPersistChanges,
							Brightness?.Level,
							[],
							zoneEffects.DrainToImmutable()
						),
						cancellationToken
					);
					ShouldPersistChanges = false;
				}
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
		UseUnifiedLighting = IsUnifiedLightingInitiallyEnabled;
		if (Brightness is { } brightness) brightness.Reset();
		if (UnifiedLightingZone is not null)
		{
			if (UnifiedLightingZone.IsChanged)
			{
				UnifiedLightingZone.Reset();
			}
		}
		foreach (var zone in LightingZones)
		{
			if (zone.IsChanged)
			{
				zone.Reset();
			}
		}
	}

	protected override void OnChanged(bool isChanged)
	{
		_applyChangesCommand.OnChanged();
		_resetChangesCommand.OnChanged();
		NotifyPropertyChanged(ChangedProperty.IsChanged);
	}

	public LightingZoneViewModel GetLightingZone(Guid zoneId) => _lightingZoneById[zoneId];

	public void OnDeviceConfigurationUpdated(in LightingDeviceConfiguration configuration)
	{
		IsUnifiedLightingInitiallyEnabled = configuration.IsUnifiedLightingEnabled;
		if (Brightness is { } vm && configuration.BrightnessLevel is not null)
		{
			vm.SetInitialBrightness(configuration.BrightnessLevel.GetValueOrDefault());
		}
		foreach (var lightingZoneEffect in configuration.ZoneEffects)
		{
			if (_lightingZoneById.TryGetValue(lightingZoneEffect.ZoneId, out var evm))
			{
				evm.OnEffectUpdated(lightingZoneEffect.Effect);
			}
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
