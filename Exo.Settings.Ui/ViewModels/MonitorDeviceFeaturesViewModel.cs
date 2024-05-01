using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class MonitorDeviceFeaturesViewModel : ChangeableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly SettingsServiceConnectionManager _connectionManager;
	private MonitorDeviceSettingViewModel? _brightnessSetting;
	private MonitorDeviceSettingViewModel? _contrastSetting;
	private MonitorDeviceSettingViewModel? _audioVolumeSetting;

	private bool _isReady;

	public MonitorDeviceSettingViewModel? BrightnessSetting => _brightnessSetting;
	public MonitorDeviceSettingViewModel? ContrastSetting => _contrastSetting;
	public MonitorDeviceSettingViewModel? AudioVolumeSetting => _audioVolumeSetting;

	public bool IsReady
	{
		get => !_isReady;
		private set => SetValue(ref _isReady, !value, ChangedProperty.IsNotBusy);
	}

	public MonitorDeviceFeaturesViewModel(DeviceViewModel device, SettingsServiceConnectionManager connectionManager)
	{
		_device = device;
		_connectionManager = connectionManager;
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
		case MonitorSetting.AudioVolume:
			UpdateSetting(settingValue, ref _audioVolumeSetting, nameof(AudioVolumeSetting));
			break;
		}
	}

	private void UpdateSetting(MonitorSettingValue settingValue, ref MonitorDeviceSettingViewModel? setting, string propertyName)
	{
		if (setting is null)
		{
			setting = new ContinuousMonitorDeviceSettingViewModel(settingValue.Setting, settingValue.CurrentValue, settingValue.MinimumValue, settingValue.MaximumValue);
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
			try { await ApplyChangeIfNeededAsync(_brightnessSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_contrastSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_audioVolumeSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }

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

	private ValueTask ApplyChangeIfNeededAsync(MonitorDeviceSettingViewModel? setting, CancellationToken cancellationToken)
		=> setting?.IsChanged == true ? ApplyChangeAsync(setting, cancellationToken): ValueTask.CompletedTask;

	private async ValueTask ApplyChangeAsync(MonitorDeviceSettingViewModel setting, CancellationToken cancellationToken)
	{
		var monitorService = await _connectionManager.GetMonitorServiceAsync(cancellationToken);
		await setting.ApplyChangeAsync(monitorService, _device.Id, cancellationToken);
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

internal abstract class MonitorDeviceSettingViewModel : ChangeableBindableObject
{
	public abstract MonitorSetting Setting { get; }
	internal abstract void SetValues(ushort currentValue, ushort minimumValue, ushort maximumValue);
	internal abstract ValueTask ApplyChangeAsync(IMonitorService monitorService, Guid deviceId, CancellationToken cancellationToken);
	public abstract void Reset();
}

internal sealed class ContinuousMonitorDeviceSettingViewModel : MonitorDeviceSettingViewModel
{ 
	private ushort _value;
	private ushort _initialValue;
	private ushort _minimumValue;
	private ushort _maximumValue;

	public override bool IsChanged => _value != _initialValue;

	public override MonitorSetting Setting { get; }

	public string DisplayName => Setting switch
	{
		MonitorSetting.Brightness => "Brightness",
		MonitorSetting.Contrast => "Contrast",
		MonitorSetting.AudioVolume => "Audio Volume",
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

	public ContinuousMonitorDeviceSettingViewModel(MonitorSetting setting, ushort currentValue, ushort minimumValue, ushort maximumValue)
	{
		_value = currentValue;
		_initialValue = currentValue;
		_minimumValue = minimumValue;
		_maximumValue = maximumValue;
		Setting = setting;
	}

	internal override void SetValues(ushort currentValue, ushort minimumValue, ushort maximumValue)
	{
		InitialValue = currentValue;
		MinimumValue = minimumValue;
		MaximumValue = maximumValue;
	}

	internal override ValueTask ApplyChangeAsync(IMonitorService monitorService, Guid deviceId, CancellationToken cancellationToken)
		=> monitorService.SetSettingValueAsync(new MonitorSettingUpdate { DeviceId = deviceId, Setting = Setting, Value = Value }, cancellationToken);

	public override void Reset() => Value = InitialValue;
}
