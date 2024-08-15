using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Exo.Contracts.Ui.Settings;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class MouseDeviceFeaturesViewModel : ApplicableResettableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly IMouseService _mouseService;
	private bool _isAvailable;
	private MouseCapabilities _dpiCapabilities;
	private DpiViewModel? _currentDpi;
	private DpiViewModel? _maximumDpi;
	private byte _minimumPresetCount;
	private byte _maximumPresetCount;
	private byte? _activeDpiPresetIndex;
	private int _selectedDpiPresetIndex;
	private int _changedPresetCount;
	private readonly ReadOnlyObservableCollection<MouseDpiPresetViewModel> _readOnlyDpiPresets;
	private readonly ObservableCollection<MouseDpiPresetViewModel> _dpiPresets;
	private readonly Dictionary<ushort, PollingFrequencyViewModel> _supportedPollingFrequencies;
	private ReadOnlyCollection<PollingFrequencyViewModel> _supportedPollingFrequencyCollection;
	private ImmutableArray<DotsPerInch> _initialDpiPresets;
	private PollingFrequencyViewModel? _initialPollingFrequency;
	private PollingFrequencyViewModel? _selectedPollingFrequency;

	public MouseDeviceFeaturesViewModel(IMouseService mouseService, DeviceViewModel device)
	{
		_mouseService = mouseService;
		_device = device;
		_initialDpiPresets = [];
		_dpiPresets = new();
		_selectedDpiPresetIndex = -1;
		_readOnlyDpiPresets = new(_dpiPresets);
		_supportedPollingFrequencies = new();
		_supportedPollingFrequencyCollection = ReadOnlyCollection<PollingFrequencyViewModel>.Empty;
	}

	public DpiViewModel? CurrentDpi => _currentDpi;

	public DpiViewModel? MaximumDpi
	{
		get => _maximumDpi;
		private set => SetValue(ref _maximumDpi, value, ChangedProperty.MaximumDpi);
	}

	public byte MinimumPresetCount
	{
		get => _minimumPresetCount;
		private set => SetValue(ref _minimumPresetCount, value);
	}

	public byte MaximumPresetCount
	{
		get => _maximumPresetCount;
		private set => SetValue(ref _maximumPresetCount, value);
	}

	public bool AllowsIndependentDpi => (_dpiCapabilities & MouseCapabilities.SeparateXYDpi) != 0;

	public bool IsAvailable
	{
		get => _isAvailable;
		private set => SetValue(ref _isAvailable, value, ChangedProperty.IsAvailable);
	}

	public MouseDpiPresetViewModel? SelectedDpiPreset => (uint)_selectedDpiPresetIndex < (uint)_dpiPresets.Count ? _dpiPresets[_selectedDpiPresetIndex] : null;

	public int SelectedDpiPresetIndex
	{
		get => _selectedDpiPresetIndex;
		set
		{
			if (SetChangeableValue(ref _selectedDpiPresetIndex, value, ChangedProperty.SelectedDpiPresetIndex))
			{
				NotifyPropertyChanged(ChangedProperty.SelectedDpiPreset);
			}
		}
	}

	public bool CanEditPresets => (_dpiCapabilities & MouseCapabilities.ConfigurableDpiPresets) != 0;
	public bool HasPresets => (_dpiCapabilities & MouseCapabilities.DpiPresets) != 0;
	public bool CanChangePollingFrequency => (_dpiCapabilities & MouseCapabilities.ConfigurablePollingFrequency) != 0 && _supportedPollingFrequencies.Count > 0;

	public ReadOnlyObservableCollection<MouseDpiPresetViewModel> DpiPresets => _readOnlyDpiPresets;

	public ReadOnlyCollection<PollingFrequencyViewModel> SupportedPollingFrequencies => _supportedPollingFrequencyCollection;

	public PollingFrequencyViewModel? SelectedPollingFrequency
	{
		get => _selectedPollingFrequency;
		set => SetChangeableValue(ref _selectedPollingFrequency, value);
	}

	public override bool IsChanged
		=> _dpiPresets.Count != _initialDpiPresets.Length ||
		_changedPresetCount != 0 ||
		_initialPollingFrequency != _selectedPollingFrequency ||
		_selectedDpiPresetIndex != (_activeDpiPresetIndex is not null ? _activeDpiPresetIndex.GetValueOrDefault() : -1);

	// This determines whether we can apply the current settings.
	// It is based on IsChanged but does additional validity checks to avoid errors and notify the user that something is not correct.
	protected override bool CanApply
		=> IsChanged &&
			(_dpiCapabilities & (MouseCapabilities.ConfigurableDpiPresets | MouseCapabilities.DpiPresetChange)) != 0 &&
			_selectedDpiPresetIndex >= 0 && _selectedDpiPresetIndex < _dpiPresets.Count &&
			_dpiPresets.Count >= MinimumPresetCount && _dpiPresets.Count <= MaximumPresetCount;

	internal void UpdateInformation(MouseDeviceInformation information)
	{
		bool wasChanged = IsChanged;
		int oldSelectedPresetIndex = _selectedDpiPresetIndex;
		var oldSelectedPreset = SelectedDpiPreset;
		bool hadPresets = HasPresets;
		bool allowedIndependentDpi = AllowsIndependentDpi;
		var oldSelectedPollingFrequency = SelectedPollingFrequency;
		var oldPollingFrequencies = SupportedPollingFrequencies;
		var couldChangePollingFrequencies = CanChangePollingFrequency;
		_dpiCapabilities = information.DpiCapabilities;
		if (HasPresets != hadPresets) NotifyPropertyChanged(nameof(HasPresets));
		if (AllowsIndependentDpi != allowedIndependentDpi) NotifyPropertyChanged(nameof(AllowsIndependentDpi));
		IsAvailable = information.IsConnected;
		MaximumDpi = (information.DpiCapabilities & MouseCapabilities.DynamicDpi) != 0 ? new(information.MaximumDpi) : null;
		MinimumPresetCount = information.MinimumDpiPresetCount;
		MaximumPresetCount = information.MaximumDpiPresetCount;
		if (HasPresets)
		{
			while (_dpiPresets.Count > MaximumPresetCount)
			{
				int index = _dpiPresets.Count - 1;
				if (_dpiPresets[index].IsChanged) _changedPresetCount--;
				_dpiPresets.RemoveAt(index);
			}
		}
		else
		{
			_initialDpiPresets = [];
			_dpiPresets.Clear();
		}
		if (information.SupportedPollingFrequencies.IsDefaultOrEmpty)
		{
			_supportedPollingFrequencies.Clear();
			_initialPollingFrequency = null;
			_selectedPollingFrequency = null;
		}
		else
		{
			// Lazily build a new frequency array as soon as a single frequency is new.
			PollingFrequencyViewModel[]? newPollingFrequencies = null;
			for (int i = 0; i < information.SupportedPollingFrequencies.Length; i++)
			{
				ushort pollingFrequency = information.SupportedPollingFrequencies[i];
				if (_supportedPollingFrequencies.TryGetValue(pollingFrequency, out var vm))
				{
					if (newPollingFrequencies is not null)
					{
						newPollingFrequencies[i] = vm;
					}
				}
				else
				{
					if (newPollingFrequencies is null)
					{
						newPollingFrequencies = new PollingFrequencyViewModel[information.SupportedPollingFrequencies.Length];
						for (int j = 0; j < i; j++)
						{
							newPollingFrequencies[j] = _supportedPollingFrequencies[information.SupportedPollingFrequencies[j]];
						}
					}
					newPollingFrequencies[i] = new(pollingFrequency);
				}
			}
			// If a new array was initialized, reset the dictionary with only the in-use frequencies.
			// (As always with these things, we should expect changes to never happen after initialization, but we cannot 100% trust the backend, as it will rely on many different drivers)
			if (newPollingFrequencies is not null)
			{
				_supportedPollingFrequencies.Clear();
				foreach (var vm in newPollingFrequencies)
				{
					_supportedPollingFrequencies.Add(vm.Frequency, vm);
				}
				// Because the list of frequencies was reset, it is necessary to refresh the selected frequencies.
				if (_initialPollingFrequency is not null && !_supportedPollingFrequencies.ContainsKey(_initialPollingFrequency.Frequency))
				{
					_initialPollingFrequency = null;
				}
				if (_selectedPollingFrequency is not null && !_supportedPollingFrequencies.ContainsKey(_selectedPollingFrequency.Frequency))
				{
					_selectedPollingFrequency = null;
				}
				_supportedPollingFrequencyCollection = Array.AsReadOnly(newPollingFrequencies);
			}
		}
		if (_selectedDpiPresetIndex != oldSelectedPresetIndex) NotifyPropertyChanged(ChangedProperty.SelectedDpiPresetIndex);
		if (!ReferenceEquals(SelectedDpiPreset, oldSelectedPreset)) NotifyPropertyChanged(ChangedProperty.SelectedDpiPreset);
		if (!ReferenceEquals(SupportedPollingFrequencies, oldPollingFrequencies)) NotifyPropertyChanged(ChangedProperty.SupportedPollingFrequencies);
		if (!ReferenceEquals(SelectedPollingFrequency, oldSelectedPollingFrequency)) NotifyPropertyChanged(ChangedProperty.SelectedPollingFrequency);
		if (couldChangePollingFrequencies != CanChangePollingFrequency) NotifyPropertyChanged(ChangedProperty.CanChangePollingFrequency);
		OnChangeStateChange(wasChanged);
	}

	internal void UpdatePresets(ImmutableArray<DotsPerInch> presets)
	{
		bool wasChanged = IsChanged;
		int oldSelectedPresetIndex = _selectedDpiPresetIndex;
		var oldSelectedPreset = SelectedDpiPreset;
		_initialDpiPresets = presets;
		if (!_initialDpiPresets.IsDefault)
		{
			for (int i = 0; i < _initialDpiPresets.Length; i++)
			{
				if (i < _dpiPresets.Count)
				{
					_dpiPresets[i].UpdateInitialValue(_initialDpiPresets[i]);
				}
				else
				{
					_dpiPresets.Add(new(this, _initialDpiPresets[i]));
				}
			}
		}
		if (_selectedDpiPresetIndex != oldSelectedPresetIndex) NotifyPropertyChanged(ChangedProperty.SelectedDpiPresetIndex);
		if (!ReferenceEquals(SelectedDpiPreset, oldSelectedPreset)) NotifyPropertyChanged(ChangedProperty.SelectedDpiPreset);
		OnChangeStateChange(wasChanged);
	}

	internal void UpdateCurrentDpi(byte? activePresetIndex, DotsPerInch dpi)
	{
		bool wasChanged = IsChanged;
		int oldSelectedPresetIndex = _selectedDpiPresetIndex;
		var oldSelectedPreset = SelectedDpiPreset;

		if (_activeDpiPresetIndex != activePresetIndex)
		{
			if (_activeDpiPresetIndex is not null && _selectedDpiPresetIndex == _activeDpiPresetIndex.GetValueOrDefault() || _selectedDpiPresetIndex < 0)
			{
				_selectedDpiPresetIndex = activePresetIndex is not null ? activePresetIndex.GetValueOrDefault() : _selectedDpiPresetIndex = -1;
			}
			_activeDpiPresetIndex = activePresetIndex;
		}
		if (_currentDpi is null || _currentDpi.Horizontal != dpi.Horizontal || _currentDpi.Vertical != dpi.Vertical)
		{
			_currentDpi = new(dpi);
			NotifyPropertyChanged(ChangedProperty.CurrentDpi);
		}
		if (_selectedDpiPresetIndex != oldSelectedPresetIndex) NotifyPropertyChanged(ChangedProperty.SelectedDpiPresetIndex);
		if (!ReferenceEquals(SelectedDpiPreset, oldSelectedPreset)) NotifyPropertyChanged(ChangedProperty.SelectedDpiPreset);
		OnChangeStateChange(wasChanged);
	}

	internal void UpdateCurrentPollingFrequency(ushort pollingFrequency)
	{
		bool wasChanged = IsChanged;
		var oldSelectedPollingFrequency = SelectedPollingFrequency;

		_supportedPollingFrequencies.TryGetValue(pollingFrequency, out var newFrequency);

		if (_initialPollingFrequency != newFrequency)
		{
			if (_selectedPollingFrequency == _initialPollingFrequency) _selectedPollingFrequency = newFrequency;
			_initialPollingFrequency = newFrequency;
		}

		if (!ReferenceEquals(SelectedPollingFrequency, oldSelectedPollingFrequency)) NotifyPropertyChanged(ChangedProperty.SelectedPollingFrequency);
		OnChangeStateChange(wasChanged);
	}

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (!CanApply) return;
		if (_changedPresetCount != 0 && (_dpiCapabilities & MouseCapabilities.ConfigurableDpiPresets) != 0)
		{
			var presets = new DotsPerInch[_dpiPresets.Count];
			for (int i = 0; i < _dpiPresets.Count; i++)
			{
				var src = _dpiPresets[i];
				presets[i] = new() { Horizontal = src.Horizontal, Vertical = src.Vertical };
			}
			await _mouseService.SetDpiPresetsAsync
			(
				new()
				{
					DeviceId = _device.Id,
					ActivePresetIndex = (byte)_selectedDpiPresetIndex,
					DpiPresets = ImmutableCollectionsMarshal.AsImmutableArray(presets)
				},
				cancellationToken
			);
		}
		else if (_changedPresetCount == 0 && _selectedDpiPresetIndex != (_activeDpiPresetIndex is not null ? _activeDpiPresetIndex.GetValueOrDefault() : -1))
		{
			await _mouseService.SetActiveDpiPresetAsync(new() { DeviceId = _device.Id, ActivePresetIndex = (byte)_selectedDpiPresetIndex }, cancellationToken);
		}

		if (_selectedPollingFrequency is not null && _selectedPollingFrequency != _initialPollingFrequency)
		{
			await _mouseService.SetPollingFrequencyAsync(new() { DeviceId = _device.Id, PollingFrequency = _selectedPollingFrequency.Frequency }, cancellationToken);
		}
	}

	protected override void Reset()
	{
		SelectedDpiPresetIndex = _activeDpiPresetIndex is not null ? _activeDpiPresetIndex.GetValueOrDefault() : -1;
	}

	internal void OnPresetChanged(MouseDpiPresetViewModel preset, bool isChanged)
	{
		if (isChanged) _changedPresetCount++;
		else _changedPresetCount--;
	}
}
