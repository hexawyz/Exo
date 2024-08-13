using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Exo.Contracts.Ui.Settings;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class MouseDeviceFeaturesViewModel : BindableObject
{
	private readonly DeviceViewModel _device;
	private bool _isAvailable;
	private MouseDpiCapabilities _dpiCapabilities;
	private DpiViewModel? _currentDpi;
	private DpiViewModel? _maximumDpi;
	private byte _minimumPresetCount;
	private byte _maximumPresetCount;
	private byte? _activeDpiPresetIndex;
	private int _selectedDpiPresetIndex;
	private readonly ReadOnlyObservableCollection<MouseDpiPresetViewModel> _readOnlyDpiPresets;
	private readonly ObservableCollection<MouseDpiPresetViewModel> _dpiPresets;
	private ImmutableArray<DotsPerInch> _initialDpiPresets;

	public MouseDeviceFeaturesViewModel(DeviceViewModel device)
	{
		_device = device;
		_initialDpiPresets = [];
		_dpiPresets = new();
		_selectedDpiPresetIndex = -1;
		_readOnlyDpiPresets = new(_dpiPresets);
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

	public bool AllowsIndependentDpi => (_dpiCapabilities & MouseDpiCapabilities.SeparateXYDpi) != 0;

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
			if (SetValue(ref _selectedDpiPresetIndex, value)) NotifyPropertyChanged(ChangedProperty.SelectedDpiPreset);
		}
	}

	public bool HasPresets => (_dpiCapabilities & MouseDpiCapabilities.DpiPresets) != 0;

	public ReadOnlyObservableCollection<MouseDpiPresetViewModel> DpiPresets => _readOnlyDpiPresets;

	internal void UpdateInformation(MouseDeviceInformation information)
	{
		bool hadPresets = HasPresets;
		bool allowedIndependentDpi = AllowsIndependentDpi;
		_dpiCapabilities = information.DpiCapabilities;
		if (HasPresets != hadPresets) NotifyPropertyChanged(nameof(HasPresets));
		if (AllowsIndependentDpi != allowedIndependentDpi) NotifyPropertyChanged(nameof(AllowsIndependentDpi));
		IsAvailable = information.IsConnected;
		MaximumDpi = (information.DpiCapabilities & MouseDpiCapabilities.DynamicDpi) != 0 ? new(information.MaximumDpi) : null;
		if (HasPresets)
		{
			while (_dpiPresets.Count > MaximumPresetCount)
			{
				_dpiPresets.RemoveAt(_dpiPresets.Count - 1);
			}
		}
		else
		{
			_initialDpiPresets = [];
			_dpiPresets.Clear();
		}
	}

	internal void UpdatePresets(ImmutableArray<DotsPerInch> presets)
	{
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
		if (!ReferenceEquals(SelectedDpiPreset, oldSelectedPreset)) NotifyPropertyChanged(ChangedProperty.SelectedDpiPreset);
	}

	internal void UpdateCurrentDpi(byte? activePresetIndex, DotsPerInch dpi)
	{
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
		if (!ReferenceEquals(SelectedDpiPreset, oldSelectedPreset)) NotifyPropertyChanged(ChangedProperty.SelectedDpiPreset);
	}
}

internal sealed class MouseDpiPresetViewModel : ChangeableBindableObject
{
	private readonly MouseDeviceFeaturesViewModel _mouse;
	private ushort _horizontal;
	private ushort _horizontalInitialValue;
	private ushort _vertical;
	private ushort _verticalInitialValue;
	private bool _isIndependent;

	public MouseDpiPresetViewModel(MouseDeviceFeaturesViewModel mouse, DotsPerInch dpi)
	{
		_mouse = mouse;
		_horizontalInitialValue = _horizontal = dpi.Horizontal;
		_verticalInitialValue = _vertical = mouse.AllowsIndependentDpi ? dpi.Vertical : dpi.Horizontal;
		_isIndependent = _horizontal != _vertical;
	}

	public override bool IsChanged => _horizontal != _horizontalInitialValue || _vertical != _verticalInitialValue;

	public ushort HorizontalInitialValue => _horizontalInitialValue;
	public ushort VerticalInitialValue => _verticalInitialValue;

	public ushort Horizontal
	{
		get => _horizontal;
		set
		{
			if (!IsIndependent) SetChangeableValue(ref _vertical, value, ChangedProperty.Vertical);
			SetChangeableValue(ref _horizontal, value, ChangedProperty.Horizontal);
		}
	}

	public ushort Vertical
	{
		get => _vertical;
		set
		{
			if (!IsIndependent) SetChangeableValue(ref _horizontal, value, ChangedProperty.Horizontal);
			SetChangeableValue(ref _vertical, value, ChangedProperty.Vertical);
		}
	}

	public bool IsIndependent
	{
		get => _isIndependent;
		set
		{
			if (!_mouse.AllowsIndependentDpi) throw new InvalidOperationException("Independent DPI can not be enabled on this device.");

			if (SetValue(ref _isIndependent, value, ChangedProperty.IsIndependent) && !value)
			{
				Vertical = Horizontal;
			}
		}
	}

	// NB: I wrote the code below in the most literal way possible.
	// It works, but maybe it can be factorized a bit. Not very important for now, though.
	public void UpdateInitialValue(DotsPerInch dpi)
	{
		bool wasChanged = IsChanged;

		if (_horizontalInitialValue != dpi.Horizontal)
		{
			if (_horizontal == _horizontalInitialValue)
			{
				_horizontal = dpi.Horizontal;
				NotifyPropertyChanged(ChangedProperty.Horizontal);
			}
			_horizontalInitialValue = dpi.Horizontal;
			NotifyPropertyChanged(ChangedProperty.HorizontalInitialValue);
		}

		if (_mouse.AllowsIndependentDpi)
		{
			if (_verticalInitialValue != dpi.Vertical)
			{
				if (_vertical == _verticalInitialValue)
				{
					_vertical = dpi.Vertical;
					NotifyPropertyChanged(ChangedProperty.Vertical);
				}
				_verticalInitialValue = dpi.Vertical;
				NotifyPropertyChanged(ChangedProperty.VerticalInitialValue);
			}
			if (_horizontal != _vertical && !_isIndependent)
			{
				_isIndependent = true;
				NotifyPropertyChanged(ChangedProperty.IsIndependent);
			}
		}
		else
		{
			if (_verticalInitialValue != dpi.Horizontal)
			{
				if (_vertical == _verticalInitialValue)
				{
					_vertical = dpi.Horizontal;
					NotifyPropertyChanged(ChangedProperty.Vertical);
				}
				_verticalInitialValue = dpi.Horizontal;
				NotifyPropertyChanged(ChangedProperty.VerticalInitialValue);
			}
			if (_isIndependent)
			{
				_isIndependent = false;
				NotifyPropertyChanged(ChangedProperty.IsIndependent);
			}
		}

		OnChangeStateChange(wasChanged);
	}
}
