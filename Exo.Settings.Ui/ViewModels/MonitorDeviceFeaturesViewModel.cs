using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class MonitorDeviceFeaturesViewModel : ApplicableResettableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly ISettingsMetadataService _metadataService;
	private readonly SettingsServiceConnectionManager _connectionManager;
	private ContinuousMonitorDeviceSettingViewModel? _brightnessSetting;
	private ContinuousMonitorDeviceSettingViewModel? _contrastSetting;
	private ContinuousMonitorDeviceSettingViewModel? _audioVolumeSetting;
	private NonContinuousMonitorDeviceSettingViewModel? _inputSelectSetting;
	private ContinuousMonitorDeviceSettingViewModel? _redVideoGainSetting;
	private ContinuousMonitorDeviceSettingViewModel? _greenVideoGainSetting;
	private ContinuousMonitorDeviceSettingViewModel? _blueVideoGainSetting;
	private ContinuousMonitorDeviceSettingViewModel? _redSixAxisSaturationControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _yellowSixAxisSaturationControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _greenSixAxisSaturationControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _cyanSixAxisSaturationControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _blueSixAxisSaturationControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _magentaSixAxisSaturationControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _redSixAxisHueControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _yellowSixAxisHueControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _greenSixAxisHueControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _cyanSixAxisHueControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _blueSixAxisHueControlSetting;
	private ContinuousMonitorDeviceSettingViewModel? _magentaSixAxisHueControlSetting;
	private NonContinuousMonitorDeviceSettingViewModel? _inputLagSetting;
	private NonContinuousMonitorDeviceSettingViewModel? _responseTimeSetting;
	private ContinuousMonitorDeviceSettingViewModel? _blueLightFilterLevelSetting;
	private NonContinuousMonitorDeviceSettingViewModel? _osdLanguageSetting;
	private BooleanMonitorDeviceSettingViewModel? _powerIndicatorSetting;

	private readonly PropertyChangedEventHandler _onSettingPropertyChanged;

	private int _changedSettingCount;
	private bool _isReady;

	public ContinuousMonitorDeviceSettingViewModel? BrightnessSetting => _brightnessSetting;
	public ContinuousMonitorDeviceSettingViewModel? ContrastSetting => _contrastSetting;
	public ContinuousMonitorDeviceSettingViewModel? AudioVolumeSetting => _audioVolumeSetting;
	public ContinuousMonitorDeviceSettingViewModel? RedVideoGainSetting => _redVideoGainSetting;
	public ContinuousMonitorDeviceSettingViewModel? GreenVideoGainSetting => _greenVideoGainSetting;
	public ContinuousMonitorDeviceSettingViewModel? BlueVideoGainSetting => _blueVideoGainSetting;
	public ContinuousMonitorDeviceSettingViewModel? RedSixAxisSaturationControlSetting => _redSixAxisSaturationControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? YellowSixAxisSaturationControlSetting => _yellowSixAxisSaturationControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? GreenSixAxisSaturationControlSetting => _greenSixAxisSaturationControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? CyanSixAxisSaturationControlSetting => _cyanSixAxisSaturationControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? BlueSixAxisSaturationControlSetting => _blueSixAxisSaturationControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? MagentaSixAxisSaturationControlSetting => _magentaSixAxisSaturationControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? RedSixAxisHueControlSetting => _redSixAxisHueControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? YellowSixAxisHueControlSetting => _yellowSixAxisHueControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? GreenSixAxisHueControlSetting => _greenSixAxisHueControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? CyanSixAxisHueControlSetting => _cyanSixAxisHueControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? BlueSixAxisHueControlSetting => _blueSixAxisHueControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? MagentaSixAxisHueControlSetting => _magentaSixAxisHueControlSetting;
	public ContinuousMonitorDeviceSettingViewModel? BlueLightFilterLevelSetting => _blueLightFilterLevelSetting;
	public NonContinuousMonitorDeviceSettingViewModel? InputSelectSetting => _inputSelectSetting;
	public NonContinuousMonitorDeviceSettingViewModel? InputLagSetting => _inputLagSetting;
	public NonContinuousMonitorDeviceSettingViewModel? ResponseTimeSetting => _responseTimeSetting;
	public NonContinuousMonitorDeviceSettingViewModel? OsdLanguageSetting => _osdLanguageSetting;
	public BooleanMonitorDeviceSettingViewModel? PowerIndicatorSetting => _powerIndicatorSetting;

	public bool IsReady
	{
		get => !_isReady;
		private set => SetValue(ref _isReady, !value, ChangedProperty.IsNotBusy);
	}

	public MonitorDeviceFeaturesViewModel(DeviceViewModel device, ISettingsMetadataService metadataService, SettingsServiceConnectionManager connectionManager)
	{
		_device = device;
		_metadataService = metadataService;
		_connectionManager = connectionManager;
		_onSettingPropertyChanged = new(OnSettingPropertyChanged);
	}

	public async Task UpdateInformationAsync(MonitorInformation information, CancellationToken cancellationToken)
	{
		foreach (var setting in information.SupportedSettings)
		{
			switch (setting)
			{
			case MonitorSetting.Brightness:
				InitializeSetting(setting, ref _brightnessSetting, nameof(BrightnessSetting));
				break;
			case MonitorSetting.Contrast:
				InitializeSetting(setting, ref _contrastSetting, nameof(ContrastSetting));
				break;
			case MonitorSetting.AudioVolume:
				InitializeSetting(setting, ref _audioVolumeSetting, nameof(AudioVolumeSetting));
				break;
			case MonitorSetting.InputSelect:
				InitializeSetting(setting, ref _inputSelectSetting, nameof(InputSelectSetting));
				await _metadataService.WaitForAvailabilityAsync(cancellationToken);
				_inputSelectSetting!.UpdateNonContinuousValues(_metadataService, information.InputSelectSources);
				break;
			case MonitorSetting.VideoGainRed:
				InitializeSetting(setting, ref _redVideoGainSetting, nameof(RedVideoGainSetting));
				break;
			case MonitorSetting.VideoGainGreen:
				InitializeSetting(setting, ref _greenVideoGainSetting, nameof(GreenVideoGainSetting));
				break;
			case MonitorSetting.VideoGainBlue:
				InitializeSetting(setting, ref _blueVideoGainSetting, nameof(BlueVideoGainSetting));
				break;
			case MonitorSetting.SixAxisSaturationControlRed:
				InitializeSetting(setting, ref _redSixAxisSaturationControlSetting, nameof(RedSixAxisSaturationControlSetting));
				break;
			case MonitorSetting.SixAxisSaturationControlYellow:
				InitializeSetting(setting, ref _yellowSixAxisSaturationControlSetting, nameof(YellowSixAxisSaturationControlSetting));
				break;
			case MonitorSetting.SixAxisSaturationControlGreen:
				InitializeSetting(setting, ref _greenSixAxisSaturationControlSetting, nameof(GreenSixAxisSaturationControlSetting));
				break;
			case MonitorSetting.SixAxisSaturationControlCyan:
				InitializeSetting(setting, ref _cyanSixAxisSaturationControlSetting, nameof(CyanSixAxisSaturationControlSetting));
				break;
			case MonitorSetting.SixAxisSaturationControlBlue:
				InitializeSetting(setting, ref _blueSixAxisSaturationControlSetting, nameof(BlueSixAxisSaturationControlSetting));
				break;
			case MonitorSetting.SixAxisSaturationControlMagenta:
				InitializeSetting(setting, ref _magentaSixAxisSaturationControlSetting, nameof(MagentaSixAxisSaturationControlSetting));
				break;
			case MonitorSetting.SixAxisHueControlRed:
				InitializeSetting(setting, ref _redSixAxisHueControlSetting, nameof(RedSixAxisHueControlSetting));
				break;
			case MonitorSetting.SixAxisHueControlYellow:
				InitializeSetting(setting, ref _yellowSixAxisHueControlSetting, nameof(YellowSixAxisHueControlSetting));
				break;
			case MonitorSetting.SixAxisHueControlGreen:
				InitializeSetting(setting, ref _greenSixAxisHueControlSetting, nameof(GreenSixAxisHueControlSetting));
				break;
			case MonitorSetting.SixAxisHueControlCyan:
				InitializeSetting(setting, ref _cyanSixAxisHueControlSetting, nameof(CyanSixAxisHueControlSetting));
				break;
			case MonitorSetting.SixAxisHueControlBlue:
				InitializeSetting(setting, ref _blueSixAxisHueControlSetting, nameof(BlueSixAxisHueControlSetting));
				break;
			case MonitorSetting.SixAxisHueControlMagenta:
				InitializeSetting(setting, ref _magentaSixAxisHueControlSetting, nameof(MagentaSixAxisHueControlSetting));
				break;
			case MonitorSetting.InputLag:
				InitializeSetting(setting, ref _inputLagSetting, nameof(ResponseTimeSetting));
				await _metadataService.WaitForAvailabilityAsync(cancellationToken);
				_inputLagSetting!.UpdateNonContinuousValues(_metadataService, information.InputLagLevels);
				break;
			case MonitorSetting.ResponseTime:
				InitializeSetting(setting, ref _responseTimeSetting, nameof(ResponseTimeSetting));
				await _metadataService.WaitForAvailabilityAsync(cancellationToken);
				_responseTimeSetting!.UpdateNonContinuousValues(_metadataService, information.ResponseTimeLevels);
				break;
			case MonitorSetting.BlueLightFilterLevel:
				InitializeSetting(setting, ref _blueLightFilterLevelSetting, nameof(BlueLightFilterLevelSetting));
				break;
			case MonitorSetting.OsdLanguage:
				InitializeSetting(setting, ref _osdLanguageSetting, nameof(OsdLanguageSetting));
				await _metadataService.WaitForAvailabilityAsync(cancellationToken);
				_osdLanguageSetting!.UpdateNonContinuousValues(_metadataService, information.OsdLanguages);
				break;
			case MonitorSetting.PowerIndicator:
				InitializeSetting(setting, ref _powerIndicatorSetting, nameof(PowerIndicatorSetting));
				break;
			}
		}
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
		case MonitorSetting.InputSelect:
			UpdateSetting(settingValue, ref _inputSelectSetting, nameof(InputSelectSetting));
			break;
		case MonitorSetting.VideoGainRed:
			UpdateSetting(settingValue, ref _redVideoGainSetting, nameof(RedVideoGainSetting));
			break;
		case MonitorSetting.VideoGainGreen:
			UpdateSetting(settingValue, ref _greenVideoGainSetting, nameof(GreenVideoGainSetting));
			break;
		case MonitorSetting.VideoGainBlue:
			UpdateSetting(settingValue, ref _blueVideoGainSetting, nameof(BlueVideoGainSetting));
			break;
		case MonitorSetting.SixAxisSaturationControlRed:
			UpdateSetting(settingValue, ref _redSixAxisSaturationControlSetting, nameof(RedSixAxisSaturationControlSetting));
			break;
		case MonitorSetting.SixAxisSaturationControlYellow:
			UpdateSetting(settingValue, ref _yellowSixAxisSaturationControlSetting, nameof(YellowSixAxisSaturationControlSetting));
			break;
		case MonitorSetting.SixAxisSaturationControlGreen:
			UpdateSetting(settingValue, ref _greenSixAxisSaturationControlSetting, nameof(GreenSixAxisSaturationControlSetting));
			break;
		case MonitorSetting.SixAxisSaturationControlCyan:
			UpdateSetting(settingValue, ref _cyanSixAxisSaturationControlSetting, nameof(CyanSixAxisSaturationControlSetting));
			break;
		case MonitorSetting.SixAxisSaturationControlBlue:
			UpdateSetting(settingValue, ref _blueSixAxisSaturationControlSetting, nameof(BlueSixAxisSaturationControlSetting));
			break;
		case MonitorSetting.SixAxisSaturationControlMagenta:
			UpdateSetting(settingValue, ref _magentaSixAxisSaturationControlSetting, nameof(MagentaSixAxisSaturationControlSetting));
			break;
		case MonitorSetting.SixAxisHueControlRed:
			UpdateSetting(settingValue, ref _redSixAxisHueControlSetting, nameof(RedSixAxisHueControlSetting));
			break;
		case MonitorSetting.SixAxisHueControlYellow:
			UpdateSetting(settingValue, ref _yellowSixAxisHueControlSetting, nameof(YellowSixAxisHueControlSetting));
			break;
		case MonitorSetting.SixAxisHueControlGreen:
			UpdateSetting(settingValue, ref _greenSixAxisHueControlSetting, nameof(GreenSixAxisHueControlSetting));
			break;
		case MonitorSetting.SixAxisHueControlCyan:
			UpdateSetting(settingValue, ref _cyanSixAxisHueControlSetting, nameof(CyanSixAxisHueControlSetting));
			break;
		case MonitorSetting.SixAxisHueControlBlue:
			UpdateSetting(settingValue, ref _blueSixAxisHueControlSetting, nameof(BlueSixAxisHueControlSetting));
			break;
		case MonitorSetting.SixAxisHueControlMagenta:
			UpdateSetting(settingValue, ref _magentaSixAxisHueControlSetting, nameof(MagentaSixAxisHueControlSetting));
			break;
		case MonitorSetting.InputLag:
			UpdateSetting(settingValue, ref _inputLagSetting, nameof(InputLagSetting));
			break;
		case MonitorSetting.ResponseTime:
			UpdateSetting(settingValue, ref _responseTimeSetting, nameof(ResponseTimeSetting));
			break;
		case MonitorSetting.BlueLightFilterLevel:
			UpdateSetting(settingValue, ref _blueLightFilterLevelSetting, nameof(BlueLightFilterLevelSetting));
			break;
		case MonitorSetting.OsdLanguage:
			UpdateSetting(settingValue, ref _osdLanguageSetting, nameof(OsdLanguageSetting));
			break;
		case MonitorSetting.PowerIndicator:
			UpdateSetting(settingValue, ref _powerIndicatorSetting, nameof(PowerIndicatorSetting));
			break;
		}
	}

	private void InitializeSetting(MonitorSetting setting, ref ContinuousMonitorDeviceSettingViewModel? viewModel, string propertyName)
	{
		if (viewModel is null)
		{
			viewModel = new ContinuousMonitorDeviceSettingViewModel(setting, 0, 0, 0);
			viewModel.PropertyChanged += _onSettingPropertyChanged;
			NotifyPropertyChanged(propertyName);
		}
	}

	private void InitializeSetting(MonitorSetting setting, ref NonContinuousMonitorDeviceSettingViewModel? viewModel, string propertyName)
	{
		if (viewModel is null)
		{
			viewModel = new NonContinuousMonitorDeviceSettingViewModel(setting, 0);
			viewModel.PropertyChanged += _onSettingPropertyChanged;
			NotifyPropertyChanged(propertyName);
		}
	}

	private void InitializeSetting(MonitorSetting setting, ref BooleanMonitorDeviceSettingViewModel? viewModel, string propertyName)
	{
		if (viewModel is null)
		{
			viewModel = new BooleanMonitorDeviceSettingViewModel(setting, false);
			viewModel.PropertyChanged += _onSettingPropertyChanged;
			NotifyPropertyChanged(propertyName);
		}
	}

	private void UpdateSetting(MonitorSettingValue settingValue, ref ContinuousMonitorDeviceSettingViewModel? viewModel, string propertyName)
	{
		if (viewModel is null)
		{
			viewModel = new ContinuousMonitorDeviceSettingViewModel(settingValue.Setting, settingValue.CurrentValue, settingValue.MinimumValue, settingValue.MaximumValue);
			viewModel.PropertyChanged += _onSettingPropertyChanged;
			NotifyPropertyChanged(propertyName);
		}
		else
		{
			viewModel.SetValues(settingValue.CurrentValue, settingValue.MinimumValue, settingValue.MaximumValue);
		}
	}

	private void UpdateSetting(MonitorSettingValue settingValue, ref NonContinuousMonitorDeviceSettingViewModel? viewModel, string propertyName)
	{
		if (viewModel is null)
		{
			viewModel = new NonContinuousMonitorDeviceSettingViewModel(settingValue.Setting, settingValue.CurrentValue);
			viewModel.PropertyChanged += _onSettingPropertyChanged;
			NotifyPropertyChanged(propertyName);
		}
		else
		{
			viewModel.SetValues(settingValue.CurrentValue, settingValue.MinimumValue, settingValue.MaximumValue);
		}
	}

	private void UpdateSetting(MonitorSettingValue settingValue, ref BooleanMonitorDeviceSettingViewModel? viewModel, string propertyName)
	{
		if (viewModel is null)
		{
			viewModel = new BooleanMonitorDeviceSettingViewModel(settingValue.Setting, settingValue.CurrentValue != 0);
			viewModel.PropertyChanged += _onSettingPropertyChanged;
			NotifyPropertyChanged(propertyName);
		}
		else
		{
			viewModel.SetValues(settingValue.CurrentValue, settingValue.MinimumValue, settingValue.MaximumValue);
		}
	}

	private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.IsChanged) && sender is ChangeableBindableObject setting)
		{
			bool wasChanged = IsChanged;
			if (setting.IsChanged) _changedSettingCount++;
			else _changedSettingCount--;
			OnChangeStateChange(wasChanged);
		}
	}

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		IsReady = false;
		List<Exception>? exceptions = null;
		try
		{
			try { await ApplyChangeIfNeededAsync(_brightnessSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_contrastSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_audioVolumeSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_inputSelectSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_redVideoGainSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_greenVideoGainSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_blueVideoGainSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_redSixAxisSaturationControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_yellowSixAxisSaturationControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_greenSixAxisSaturationControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_cyanSixAxisSaturationControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_blueSixAxisSaturationControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_magentaSixAxisSaturationControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_redSixAxisHueControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_yellowSixAxisHueControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_greenSixAxisHueControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_cyanSixAxisHueControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_blueSixAxisHueControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_magentaSixAxisHueControlSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_inputLagSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_responseTimeSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_blueLightFilterLevelSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_osdLanguageSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_powerIndicatorSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }

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
		=> setting?.IsChanged == true ? ApplyChangeAsync(setting, cancellationToken) : ValueTask.CompletedTask;

	private async ValueTask ApplyChangeAsync(MonitorDeviceSettingViewModel setting, CancellationToken cancellationToken)
	{
		var monitorService = await _connectionManager.GetMonitorServiceAsync(cancellationToken);
		await setting.ApplyChangeAsync(monitorService, _device.Id, cancellationToken);
	}

	public override bool IsChanged => _changedSettingCount != 0;

	protected override void Reset()
	{
		if (!IsReady) throw new InvalidOperationException();
		IResettable.SharedResetCommand.Execute(_brightnessSetting);
		IResettable.SharedResetCommand.Execute(_contrastSetting);
		IResettable.SharedResetCommand.Execute(_audioVolumeSetting);
		IResettable.SharedResetCommand.Execute(_inputSelectSetting);
		IResettable.SharedResetCommand.Execute(_redVideoGainSetting);
		IResettable.SharedResetCommand.Execute(_greenVideoGainSetting);
		IResettable.SharedResetCommand.Execute(_blueVideoGainSetting);
		IResettable.SharedResetCommand.Execute(_redSixAxisSaturationControlSetting);
		IResettable.SharedResetCommand.Execute(_yellowSixAxisSaturationControlSetting);
		IResettable.SharedResetCommand.Execute(_greenSixAxisSaturationControlSetting);
		IResettable.SharedResetCommand.Execute(_cyanSixAxisSaturationControlSetting);
		IResettable.SharedResetCommand.Execute(_blueSixAxisSaturationControlSetting);
		IResettable.SharedResetCommand.Execute(_magentaSixAxisSaturationControlSetting);
		IResettable.SharedResetCommand.Execute(_redSixAxisHueControlSetting);
		IResettable.SharedResetCommand.Execute(_yellowSixAxisHueControlSetting);
		IResettable.SharedResetCommand.Execute(_greenSixAxisHueControlSetting);
		IResettable.SharedResetCommand.Execute(_cyanSixAxisHueControlSetting);
		IResettable.SharedResetCommand.Execute(_blueSixAxisHueControlSetting);
		IResettable.SharedResetCommand.Execute(_magentaSixAxisHueControlSetting);
		IResettable.SharedResetCommand.Execute(_inputLagSetting);
		IResettable.SharedResetCommand.Execute(_responseTimeSetting);
		IResettable.SharedResetCommand.Execute(_blueLightFilterLevelSetting);
		IResettable.SharedResetCommand.Execute(_osdLanguageSetting);
		IResettable.SharedResetCommand.Execute(_powerIndicatorSetting);
	}
}

internal abstract class MonitorDeviceSettingViewModel : ResettableBindableObject
{
	public abstract MonitorSetting Setting { get; }
	internal abstract void SetValues(ushort currentValue, ushort minimumValue, ushort maximumValue);
	internal abstract ValueTask ApplyChangeAsync(IMonitorService monitorService, Guid deviceId, CancellationToken cancellationToken);
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
		MonitorSetting.VideoGainRed => "Red Video Gain",
		MonitorSetting.VideoGainGreen => "Green Video Gain",
		MonitorSetting.VideoGainBlue => "Blue Video Gain",
		MonitorSetting.SixAxisSaturationControlRed => "Six Axis Saturation Control (Red)",
		MonitorSetting.SixAxisSaturationControlYellow => "Six Axis Saturation Control (Yellow)",
		MonitorSetting.SixAxisSaturationControlGreen => "Six Axis Saturation Control (Green)",
		MonitorSetting.SixAxisSaturationControlCyan => "Six Axis Saturation Control (Cyan)",
		MonitorSetting.SixAxisSaturationControlBlue => "Six Axis Saturation Control (Blue)",
		MonitorSetting.SixAxisSaturationControlMagenta => "Six Axis Saturation Control (Magenta)",
		MonitorSetting.SixAxisHueControlRed => "Six Axis Hue Control (Red)",
		MonitorSetting.SixAxisHueControlYellow => "Six Axis Hue Control (Yellow)",
		MonitorSetting.SixAxisHueControlGreen => "Six Axis Hue Control (Green)",
		MonitorSetting.SixAxisHueControlCyan => "Six Axis Hue Control (Cyan)",
		MonitorSetting.SixAxisHueControlBlue => "Six Axis Hue Control (Blue)",
		MonitorSetting.SixAxisHueControlMagenta => "Six Axis Hue Control (Magenta)",
		_ => Setting.ToString()
	};

	public ushort InitialValue
	{
		get => _initialValue;
		private set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _initialValue, value, ChangedProperty.InitialValue))
			{
				if (!wasChanged)
				{
					_value = _initialValue;
					NotifyPropertyChanged(ChangedProperty.Value);
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

	protected override void Reset() => Value = InitialValue;
}

internal sealed class NonContinuousMonitorDeviceSettingViewModel : MonitorDeviceSettingViewModel
{
	private ushort _value;
	private ushort _initialValue;
	private ReadOnlyCollection<NonContinuousValueViewModel> _supportedValueCollection;
	private readonly Dictionary<ushort, NonContinuousValueViewModel> _supportedValues;

	public override bool IsChanged => _value != _initialValue;

	public override MonitorSetting Setting { get; }

	public string DisplayName => Setting switch
	{
		MonitorSetting.InputSelect => "Input Select",
		MonitorSetting.OsdLanguage => "OSD Language",
		_ => Setting.ToString()
	};

	public NonContinuousValueViewModel? InitialValue
	{
		get
		{
			_supportedValues.TryGetValue(_initialValue, out var vm);
			return vm;
		}
		private set
		{
			ArgumentNullException.ThrowIfNull(value);
			if (!ReferenceEquals(value.SettingViewModel, this)) throw new ArgumentException();
			bool wasChanged = IsChanged;
			if (SetValue(ref _initialValue, value.Value, ChangedProperty.InitialValue))
			{
				if (!wasChanged)
				{
					_value = _initialValue;
					NotifyPropertyChanged(ChangedProperty.Value);
				}
				else
				{
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	public NonContinuousValueViewModel? Value
	{
		get
		{
			_supportedValues.TryGetValue(_value, out var vm);
			return vm;
		}
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			if (!ReferenceEquals(value.SettingViewModel, this)) throw new ArgumentException();
			bool wasChanged = IsChanged;
			if (SetValue(ref _value, value.Value, ChangedProperty.Value))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public ReadOnlyCollection<NonContinuousValueViewModel> SupportedValues
	{
		get => _supportedValueCollection;
		private set => SetValue(ref _supportedValueCollection, value, ChangedProperty.SupportedValues);
	}

	public NonContinuousMonitorDeviceSettingViewModel(MonitorSetting setting, ushort currentValue)
	{
		Setting = setting;
		_supportedValueCollection = ReadOnlyCollection<NonContinuousValueViewModel>.Empty;
		_supportedValues = new();
		_value = _initialValue = currentValue;
	}

	internal void UpdateNonContinuousValues(ISettingsMetadataService metadataService, ImmutableArray<NonContinuousValue> values)
	{
		if (values.IsDefaultOrEmpty) SupportedValues = ReadOnlyCollection<NonContinuousValueViewModel>.Empty;

		foreach (var valueDefinition in values)
		{
			string? friendlyName = valueDefinition.CustomName;
			if (friendlyName is null && valueDefinition.NameStringId is { } stringId)
			{
				friendlyName = metadataService.GetString(CultureInfo.CurrentCulture, stringId);
			}
			if (friendlyName is null)
			{
				friendlyName = valueDefinition.Value.ToString("X4", CultureInfo.InvariantCulture);
			}
			if (_supportedValues.TryGetValue(valueDefinition.Value, out var vm))
			{
				vm.UpdateName(friendlyName);
			}
			else
			{
				_supportedValues.Add(valueDefinition.Value, new(this, valueDefinition.Value, friendlyName));
			}
		}

		if (_supportedValueCollection.Count != values.Length || _supportedValues.Count > values.Length)
		{
			var oldInitialValue = InitialValue;
			var oldValue = Value;

			var oldValues = new HashSet<ushort>();
			foreach (var vm in _supportedValueCollection)
			{
				oldValues.Add(vm.Value);
			}
			var supportedValues = new NonContinuousValueViewModel[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				var valueDefinition = values[i];
				oldValues.Remove(valueDefinition.Value);
				supportedValues[i] = _supportedValues[valueDefinition.Value];
			}
			foreach (var value in oldValues)
			{
				_supportedValues.Remove(value);
			}
			SupportedValues = Array.AsReadOnly(supportedValues);

			if (!ReferenceEquals(oldInitialValue, InitialValue)) NotifyPropertyChanged(ChangedProperty.InitialValue);
			if (!ReferenceEquals(oldValue, Value)) NotifyPropertyChanged(ChangedProperty.Value);
		}
	}

	internal override void SetValues(ushort currentValue, ushort minimumValue, ushort maximumValue)
	{
		var initialValue = _supportedValues.TryGetValue(currentValue, out var valueViewModel) ?
			valueViewModel :
			new NonContinuousValueViewModel(this, currentValue, currentValue.ToString("X4", CultureInfo.InvariantCulture));
		InitialValue = initialValue;
	}

	internal override ValueTask ApplyChangeAsync(IMonitorService monitorService, Guid deviceId, CancellationToken cancellationToken)
		=> Value is { } value ?
			monitorService.SetSettingValueAsync(new MonitorSettingUpdate { DeviceId = deviceId, Setting = Setting, Value = Value.Value }, cancellationToken) :
			ValueTask.CompletedTask;

	protected override void Reset()
	{
		if (IsChanged)
		{
			_value = _initialValue;
			NotifyPropertyChanged(ChangedProperty.Value);
			OnChangeStateChange(true);
		}
	}
}

internal sealed class NonContinuousValueViewModel : BindableObject
{
	internal NonContinuousMonitorDeviceSettingViewModel SettingViewModel { get; }
	public ushort Value { get; }
	private string _friendlyName;

	public NonContinuousValueViewModel(NonContinuousMonitorDeviceSettingViewModel settingViewModel, ushort value, string friendlyName)
	{
		SettingViewModel = settingViewModel;
		Value = value;
		_friendlyName = friendlyName;
	}

	public string FriendlyName => _friendlyName;

	internal void UpdateName(string name)
	{
		SetValue(ref _friendlyName, name, ChangedProperty.FriendlyName);
	}
}

internal sealed class BooleanMonitorDeviceSettingViewModel : MonitorDeviceSettingViewModel
{
	private bool _value;
	private bool _initialValue;

	public override bool IsChanged => _value != _initialValue;

	public override MonitorSetting Setting { get; }

	public string DisplayName => Setting switch
	{
		MonitorSetting.PowerIndicator => "Power Indicator",
		_ => Setting.ToString()
	};

	public bool InitialValue
	{
		get => _initialValue;
		private set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _initialValue, value, ChangedProperty.InitialValue))
			{
				if (!wasChanged)
				{
					_value = _initialValue;
					NotifyPropertyChanged(ChangedProperty.Value);
				}
				else
				{
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	public bool Value
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

	public BooleanMonitorDeviceSettingViewModel(MonitorSetting setting, bool currentValue)
	{
		Setting = setting;
		_value = _initialValue = currentValue;
	}

	internal override void SetValues(ushort currentValue, ushort minimumValue, ushort maximumValue)
		=> InitialValue = currentValue != 0;

	internal override ValueTask ApplyChangeAsync(IMonitorService monitorService, Guid deviceId, CancellationToken cancellationToken)
		=> monitorService.SetSettingValueAsync(new MonitorSettingUpdate { DeviceId = deviceId, Setting = Setting, Value = Value ? (ushort)1 : (ushort)0 }, cancellationToken);

	protected override void Reset() => Value = InitialValue;
}
