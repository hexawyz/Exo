using Exo.Ui;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class MonitorDeviceFeaturesViewModel : ChangeableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly IMonitorService _monitorService;
	private MonitorDeviceSettingViewModel? _brightnessSetting;
	private MonitorDeviceSettingViewModel? _contrastSetting;

	private bool _isReady;

	public MonitorDeviceSettingViewModel? BrightnessSetting => _brightnessSetting;
	public MonitorDeviceSettingViewModel? ContrastSetting => _contrastSetting;

	public bool IsReady
	{
		get => !_isReady;
		private set => SetValue(ref _isReady, !value, ChangedProperty.IsNotBusy);
	}

	public MonitorDeviceFeaturesViewModel(DeviceViewModel device, IMonitorService monitorService)
	{
		_device = device;
		_monitorService = monitorService;
	}

	public void UpdateSetting(MonitorSettingValue settingValue)
	{
		switch (settingValue.Setting)
		{
		case MonitorSetting.Brightness:
			UpdateSetting(settingValue, ref _brightnessSetting, nameof(BrightnessSetting));
			break;
		case MonitorSetting.Contrast:
			UpdateSetting(settingValue, ref _contrastSetting, nameof(ContrastSetting));
			break;
		}
	}

	private void UpdateSetting(MonitorSettingValue settingValue, ref MonitorDeviceSettingViewModel? setting, string propertyName)
	{
		if (setting is null)
		{
			setting = new(settingValue.Setting, settingValue.CurrentValue, settingValue.MinimumValue, settingValue.MaximumValue);
			NotifyPropertyChanged(propertyName);
		}
		else
		{
			setting.SetValues(settingValue.CurrentValue, settingValue.MinimumValue, settingValue.MaximumValue);
		}
	}

	public async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		IsReady = false;
		List<Exception>? exceptions = null;
		try
		{
			if (_brightnessSetting is not null)
			{
				try
				{
					await _monitorService.SetBrightnessAsync(new() { DeviceId = _device.Id, Value = _brightnessSetting.Value }, cancellationToken);
				}
				catch (Exception ex)
				{
					(exceptions ??= []).Add(ex);
				}
			}
			if (_contrastSetting is not null)
			{
				try
				{
					await _monitorService.SetContrastAsync(new() { DeviceId = _device.Id, Value = _contrastSetting.Value }, cancellationToken);
				}
				catch (Exception ex)
				{
					(exceptions ??= []).Add(ex);
				}
			}

			if (exceptions is not null)
			{
				throw new AggregateException(exceptions);
			}
		}
		finally
		{
			IsReady = true;
		}
	}

	public void Reset()
	{
		if (!IsReady) throw new InvalidOperationException();
		_brightnessSetting?.Reset();
		_contrastSetting?.Reset();
	}

	// TODO
	public override bool IsChanged => true;
}

internal sealed class MonitorDeviceSettingViewModel : ChangeableBindableObject
{ 
	private ushort _value;
	private ushort _initialValue;
	private ushort _minimumValue;
	private ushort _maximumValue;

	public override bool IsChanged => _value != _initialValue;

	public MonitorSetting Setting { get; }

	public string DisplayName => Setting switch
	{
		MonitorSetting.Brightness => "Brightness",
		MonitorSetting.Contrast => "Contrast",
		_ => Setting.ToString()
	};

	public ushort InitialValue
	{
		get => _initialValue;
		set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _initialValue, value, ChangedProperty.InitialValue))
			{
				if (!wasChanged)
				{
					_value = _initialValue;
				}
				else
				{
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	public ushort Value
	{
		get => _value;
		set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _value, value, ChangedProperty.Value))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public ushort MinimumValue
	{
		get => _minimumValue;
		set => SetValue(ref _minimumValue, value, ChangedProperty.MinimumValue);
	}

	public ushort MaximumValue
	{
		get => _maximumValue;
		set => SetValue(ref _maximumValue, value, ChangedProperty.MaximumValue);
	}

	public MonitorDeviceSettingViewModel(MonitorSetting setting, ushort currentValue, ushort minimumValue, ushort maximumValue)
	{
		_value = currentValue;
		_initialValue = currentValue;
		_minimumValue = minimumValue;
		_maximumValue = maximumValue;
		Setting = setting;
	}

	internal void SetValues(ushort currentValue, ushort minimumValue, ushort maximumValue)
	{
		InitialValue = currentValue;
		MinimumValue = minimumValue;
		MaximumValue = maximumValue;
	}

	public void Reset() => Value = InitialValue;
}
