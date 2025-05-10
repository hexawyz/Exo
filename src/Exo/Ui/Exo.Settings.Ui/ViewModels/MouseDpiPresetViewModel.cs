using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class MouseDpiPresetViewModel : ChangeableBindableObject
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

	public bool CanEditPresets => _mouse.CanEditPresets;

	public DpiViewModel? MaximumDpi => _mouse.MaximumDpi;

	public ushort HorizontalInitialValue => _horizontalInitialValue;
	public ushort VerticalInitialValue => _verticalInitialValue;

	public ushort Horizontal
	{
		get => _horizontal;
		set
		{
			if (_mouse.MaximumDpi is { } maxDpi && value <= maxDpi.Horizontal)
			{
				if (!IsIndependent) SetChangeableValue(ref _vertical, value, ChangedProperty.Vertical);
				SetChangeableValue(ref _horizontal, value, ChangedProperty.Horizontal);
			}
		}
	}

	public ushort Vertical
	{
		get => _vertical;
		set
		{
			if (_mouse.MaximumDpi is { } maxDpi && value <= maxDpi.Vertical)
			{
				if (!IsIndependent) SetChangeableValue(ref _horizontal, value, ChangedProperty.Horizontal);
				SetChangeableValue(ref _vertical, value, ChangedProperty.Vertical);
			}
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

	public void Reset()
	{
		if (IsChanged)
		{
			if (_horizontal != _horizontalInitialValue)
			{
				_horizontal = _horizontalInitialValue;
				NotifyPropertyChanged(ChangedProperty.Horizontal);
			}
			if (_vertical != _verticalInitialValue)
			{
				_vertical = _verticalInitialValue;
				NotifyPropertyChanged(ChangedProperty.Vertical);
			}
			OnChanged(false);
		}
		IsIndependent = _horizontal != _vertical;
	}

	protected override void OnChanged(bool isChanged)
	{
		_mouse.OnPresetChanged(this, isChanged);
		base.OnChanged(isChanged);
	}
}
