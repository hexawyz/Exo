using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.Lighting;
using Exo.Service;
using Exo.Settings.Ui.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceViewModel : ChangeableBindableObject, IDisposable
{
	private readonly DeviceViewModel _deviceViewModel;

	public LightingViewModel LightingViewModel { get; }
	private LightingZoneViewModel? _unifiedLightingZone;
	private readonly ObservableCollection<LightingZoneViewModel> _lightingZones;
	private readonly ReadOnlyObservableCollection<LightingZoneViewModel> _readOnlyLightingZones;
	private LightingDeviceBrightnessCapabilitiesViewModel? _brightnessCapabilities;
	private LightingDeviceBrightnessViewModel? _brightness;

	private readonly Dictionary<Guid, LightingZoneViewModel> _lightingZoneById;

	private LightingPersistenceMode _persistenceMode;

	private int _changedZoneCount;
	private int _busyZoneCount;
	private bool _useUnifiedLighting;
	private bool _useUnifiedLightingInitialValue;
	private bool _isExpanded;
	private bool _shouldPersistChanges;

	private readonly Commands.ApplyChangesCommand _applyChangesCommand;
	private readonly Commands.ResetChangesCommand _resetChangesCommand;

	private readonly INotificationSystem _notificationSystem;
	private readonly ILogger<LightingDeviceViewModel> _logger;

	public bool IsNotBusy => _busyZoneCount == 0;

	public override bool IsChanged => AreZonesChanged || IsBrightnessChanged || IsUseUnifiedLightingChanged || _shouldPersistChanges;

	private bool AreZonesChanged => _changedZoneCount != 0;
	private bool IsBrightnessChanged => Brightness?.IsChanged == true;
	private bool IsUseUnifiedLightingChanged => _useUnifiedLighting != _useUnifiedLightingInitialValue;

	public LightingZoneViewModel? UnifiedLightingZone => _unifiedLightingZone;
	public ReadOnlyObservableCollection<LightingZoneViewModel> LightingZones => _readOnlyLightingZones;
	public LightingDeviceBrightnessCapabilitiesViewModel? BrightnessCapabilities => _brightnessCapabilities;
	public LightingDeviceBrightnessViewModel? Brightness => _brightness;

	public bool CanToggleUnifiedLighting => _unifiedLightingZone is not null && _lightingZones.Count > 0;

	public bool UseUnifiedLighting
	{
		get => _useUnifiedLighting;
		set
		{
			bool wasChanged = IsChanged;
			if (value)
			{
				if (_unifiedLightingZone is null) throw new InvalidOperationException("This device does not support unified lighting.");
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

	public LightingDeviceViewModel(ILogger<LightingDeviceViewModel> logger, LightingViewModel lightingViewModel, DeviceViewModel deviceViewModel, LightingDeviceInformation lightingDeviceInformation, INotificationSystem notificationSystem)
	{
		_logger = logger;
		_notificationSystem = notificationSystem;
		_deviceViewModel = deviceViewModel;
		LightingViewModel = lightingViewModel;
		_applyChangesCommand = new(this);
		_resetChangesCommand = new(this);
		_persistenceMode = lightingDeviceInformation.PersistenceMode;
		if (lightingDeviceInformation.UnifiedLightingZone is { } unifiedLightingZone)
		{
			var (displayName, displayOrder, componentType, shape) = LightingViewModel.GetZoneMetadata(unifiedLightingZone.ZoneId);
			_unifiedLightingZone = new(this, unifiedLightingZone, displayName, displayOrder, componentType, shape);
		}
		_lightingZones = new();
		if (!lightingDeviceInformation.LightingZones.IsDefault)
		{
			foreach (var lightingZone in lightingDeviceInformation.LightingZones)
			{
				var (displayName, displayOrder, componentType, shape) = LightingViewModel.GetZoneMetadata(lightingZone.ZoneId);
				var vm = new LightingZoneViewModel(this, lightingZone, displayName, displayOrder, componentType, shape);
				_lightingZones.Insert(IOrderable.FindInsertPosition(_lightingZones, displayOrder), vm);
			}
		}
		_readOnlyLightingZones = new(_lightingZones);
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
			_brightnessCapabilities = new(brightnessCapabilities);
			_brightness = new(brightnessCapabilities);
			_brightness.PropertyChanged += OnBrightnessPropertyChanged;
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

	// So, basically, lighting devices should never change when the service is running, but since the service persists every detail,
	// a device can come up later on with updated settings, likely but not necessarily after a driver update. (A controller could have more or less connected devices)
	internal void UpdateInformation(LightingDeviceInformation information)
	{
		if (information.BrightnessCapabilities is { } brightnessCapabilities)
		{
			if (_brightnessCapabilities is null || _brightnessCapabilities.MinimumLevel != brightnessCapabilities.MinimumValue || _brightnessCapabilities.MaximumLevel != brightnessCapabilities.MaximumValue)
			{
				_brightnessCapabilities = new(brightnessCapabilities);
				NotifyPropertyChanged(nameof(BrightnessCapabilities));
			}
			if (_brightness is null)
			{
				_brightness = new(brightnessCapabilities);
				_brightness.PropertyChanged += OnBrightnessPropertyChanged;
				NotifyPropertyChanged(nameof(Brightness));
			}
			else
			{
				_brightness.UpdateInformation(brightnessCapabilities);
			}
		}
		else
		{
			if (_brightnessCapabilities is not null)
			{
				_brightnessCapabilities = null;
				NotifyPropertyChanged(nameof(BrightnessCapabilities));
			}
			if (_brightness is not null)
			{
				_brightness.PropertyChanged -= OnBrightnessPropertyChanged;
				_brightness = null;
				NotifyPropertyChanged(nameof(Brightness));
			}
		}

		if (_persistenceMode != information.PersistenceMode)
		{
			bool canPersistChanges = CanPersistChanges;
			_persistenceMode = information.PersistenceMode;
			if (canPersistChanges != CanPersistChanges) NotifyPropertyChanged(nameof(CanPersistChanges));
		}

		// TODO: Palette

		if (information.UnifiedLightingZone is { } unifiedLightingZone)
		{
			if (_unifiedLightingZone is null || _unifiedLightingZone.Id != unifiedLightingZone.ZoneId)
			{
				var (displayName, displayOrder, componentType, shape) = LightingViewModel.GetZoneMetadata(unifiedLightingZone.ZoneId);
				if (_unifiedLightingZone is not null) ClearUnifiedLightingZone();
				var vm = new LightingZoneViewModel(this, unifiedLightingZone, displayName, displayOrder, componentType, shape);
				_unifiedLightingZone = vm;
				_unifiedLightingZone.PropertyChanged += OnLightingZonePropertyChanged;
				_lightingZoneById.Add(vm.Id, vm);
				NotifyPropertyChanged(nameof(UnifiedLightingZone));
			}
		}
		else if (_unifiedLightingZone is not null)
		{
			ClearUnifiedLightingZone();
			NotifyPropertyChanged(nameof(UnifiedLightingZone));
		}

		// To somewhat reduce the number of operation, it is better to first remove all lighting zones that need to be removed.
		// Most of the time, if there is any change at all, there will be nothing to remove.
		// I don't think there is a good way to avoid this dictionary, though, but that is probably ok.
		var lightingZoneByIndex = new Dictionary<Guid, int>();
		for (int i = 0; i < _lightingZones.Count; i++)
		{
			lightingZoneByIndex.Add(_lightingZones[i].Id, i);
		}

		foreach (var lightingZone in information.LightingZones)
		{
			lightingZoneByIndex.Remove(lightingZone.ZoneId);
		}

		// Once all the lighting zones to remove are identified, we can remove them one by one, starting from the end of the array.
		// Starting from the end allows for optimizing the remove process by minimizing the number of items moved, but more importantly, it allows using the index we have already noted earlier.
		if (lightingZoneByIndex.Count > 0)
		{
			var indicesToRemove = lightingZoneByIndex.Values.ToArray();
			Array.Sort(indicesToRemove);
			for (int i = indicesToRemove.Length; --i > 0;)
			{
				int index = indicesToRemove[i];
				var vm = _lightingZones[index];
				if (vm.IsChanged) --_changedZoneCount;
				_lightingZoneById.Remove(vm.Id);
				_lightingZones.RemoveAt(index);
			}
		}

		// Update previous lighting zones or add new ones.
		foreach (var lightingZone in information.LightingZones)
		{
			if (_lightingZoneById.TryGetValue(lightingZone.ZoneId, out var vm))
			{
				vm.UpdateInformation(lightingZone);
			}
			else
			{
				var (displayName, displayOrder, componentType, shape) = LightingViewModel.GetZoneMetadata(lightingZone.ZoneId);
				vm = new LightingZoneViewModel(this, lightingZone, displayName, displayOrder, componentType, shape);
				_lightingZones.Insert(IOrderable.FindInsertPosition(_lightingZones, displayOrder), vm);
				vm.PropertyChanged += OnLightingZonePropertyChanged;
			}
		}

		void ClearUnifiedLightingZone()
		{
			_lightingZoneById.Remove(_unifiedLightingZone.Id);
			if (_unifiedLightingZone.IsChanged) --_changedZoneCount;
			_unifiedLightingZone.PropertyChanged -= OnLightingZonePropertyChanged;
			_unifiedLightingZone = null;
		}
	}

	private void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.FriendlyName) || Equals(e, ChangedProperty.Category) || Equals(e, ChangedProperty.IsAvailable))
		{
			NotifyPropertyChanged(e);
		}
	}

	private void OnBrightnessPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.IsChanged))
		{
			bool isChanged = ((LightingDeviceBrightnessViewModel)sender!).IsChanged;

			if (isChanged != (AreZonesChanged || IsUseUnifiedLightingChanged || _shouldPersistChanges))
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
				if (!(IsBrightnessChanged || IsUseUnifiedLightingChanged || _shouldPersistChanges))
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
		catch (Exception ex)
		{
			_notificationSystem.PublishError("Failed to apply lighting changes.", $"Applying lighting changes for device {_deviceViewModel.FriendlyName} failed: {ex.Message}");
			_logger.LightingApplyError(_deviceViewModel.FriendlyName, ex);
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
