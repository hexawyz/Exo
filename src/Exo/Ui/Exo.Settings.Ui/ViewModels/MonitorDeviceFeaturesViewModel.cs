using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.Monitors;
using Exo.Service;
using Exo.Settings.Ui.Services;
using Exo.Ui;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

internal sealed partial class MonitorDeviceFeaturesViewModel : ApplicableResettableBindableObject, IRefreshable
{
	private readonly DeviceViewModel _device;
	private readonly ISettingsMetadataService _metadataService;
	private readonly IMonitorService _monitorService;
	private ContinuousMonitorDeviceSettingViewModel? _brightnessSetting;
	private ContinuousMonitorDeviceSettingViewModel? _contrastSetting;
	private ContinuousMonitorDeviceSettingViewModel? _sharpnessSetting;
	private ContinuousMonitorDeviceSettingViewModel? _blueLightFilterLevelSetting;
	private ContinuousMonitorDeviceSettingViewModel? _audioVolumeSetting;
	private NonContinuousMonitorDeviceSettingViewModel? _inputSelectSetting;
	private ContinuousMonitorDeviceSettingViewModel? _redVideoGainSetting;
	private ContinuousMonitorDeviceSettingViewModel? _greenVideoGainSetting;
	private ContinuousMonitorDeviceSettingViewModel? _blueVideoGainSetting;
	private ContinuousMonitorDeviceSettingViewModel? _redVideoBlackLevelSetting;
	private ContinuousMonitorDeviceSettingViewModel? _greenVideoBlackLevelSetting;
	private ContinuousMonitorDeviceSettingViewModel? _blueVideoBlackLevelSetting;
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
	private NonContinuousMonitorDeviceSettingViewModel? _osdLanguageSetting;
	private BooleanMonitorDeviceSettingViewModel? _powerIndicatorSetting;

	private readonly PropertyChangedEventHandler _onSettingPropertyChanged;

	private int _changedSettingCount;
	private bool _isReady;
	private bool _isExpanded;
	private bool _isPerformanceSectionExpanded;
	private bool _hasPerformanceSection;
	private bool _isRgbSectionExpanded;
	private bool _hasRgbSection;
	private bool _isSixAxisSectionExpanded;
	private bool _hasSixAxisSection;
	private bool _isMiscellaneousSectionExpanded;
	private bool _hasMiscellaneousSection;

	private long _lastRefreshTimestamp;

	public ContinuousMonitorDeviceSettingViewModel? BrightnessSetting => _brightnessSetting;
	public ContinuousMonitorDeviceSettingViewModel? ContrastSetting => _contrastSetting;
	public ContinuousMonitorDeviceSettingViewModel? SharpnessSetting => _sharpnessSetting;
	public ContinuousMonitorDeviceSettingViewModel? BlueLightFilterLevelSetting => _blueLightFilterLevelSetting;
	public ContinuousMonitorDeviceSettingViewModel? AudioVolumeSetting => _audioVolumeSetting;
	public ContinuousMonitorDeviceSettingViewModel? RedVideoGainSetting => _redVideoGainSetting;
	public ContinuousMonitorDeviceSettingViewModel? GreenVideoGainSetting => _greenVideoGainSetting;
	public ContinuousMonitorDeviceSettingViewModel? BlueVideoGainSetting => _blueVideoGainSetting;
	public ContinuousMonitorDeviceSettingViewModel? RedVideoBlackLevelSetting => _redVideoBlackLevelSetting;
	public ContinuousMonitorDeviceSettingViewModel? GreenVideoBlackLevelSetting => _greenVideoBlackLevelSetting;
	public ContinuousMonitorDeviceSettingViewModel? BlueVideoBlackLevelSetting => _blueVideoBlackLevelSetting;
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
	public NonContinuousMonitorDeviceSettingViewModel? InputSelectSetting => _inputSelectSetting;
	public NonContinuousMonitorDeviceSettingViewModel? InputLagSetting => _inputLagSetting;
	public NonContinuousMonitorDeviceSettingViewModel? ResponseTimeSetting => _responseTimeSetting;
	public NonContinuousMonitorDeviceSettingViewModel? OsdLanguageSetting => _osdLanguageSetting;
	public BooleanMonitorDeviceSettingViewModel? PowerIndicatorSetting => _powerIndicatorSetting;

	public bool IsReady
	{
		get => _isReady;
		private set
		{
			if (SetValue(ref _isReady, value, ChangedProperty.IsReady))
			{
				IRefreshable.NotifyCanExecuteChanged();
			}
		}
	}

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	public bool IsPerformanceSectionExpanded
	{
		get => _isPerformanceSectionExpanded;
		set => SetValue(ref _isPerformanceSectionExpanded, value);
	}

	public bool HasPerformanceSection
	{
		get => _hasPerformanceSection;
		private set => SetValue(ref _hasPerformanceSection, value);
	}

	public bool IsRgbSectionExpanded
	{
		get => _isRgbSectionExpanded;
		set => SetValue(ref _isRgbSectionExpanded, value);
	}

	public bool HasRgbSection
	{
		get => _hasRgbSection;
		private set => SetValue(ref _hasRgbSection, value);
	}

	public bool IsSixAxisSectionExpanded
	{
		get => _isSixAxisSectionExpanded;
		set => SetValue(ref _isSixAxisSectionExpanded, value);
	}

	public bool HasSixAxisSection
	{
		get => _hasSixAxisSection;
		private set => SetValue(ref _hasSixAxisSection, value);
	}

	public bool IsMiscellaneousSectionExpanded
	{
		get => _isMiscellaneousSectionExpanded;
		set => SetValue(ref _isMiscellaneousSectionExpanded, value);
	}

	public bool HasMiscellaneousSection
	{
		get => _hasMiscellaneousSection;
		private set => SetValue(ref _hasMiscellaneousSection, value);
	}

	public ICommand RefreshCommand => IRefreshable.SharedRefreshCommand;

	public MonitorDeviceFeaturesViewModel(ITypedLoggerProvider loggerProvider, DeviceViewModel device, ISettingsMetadataService metadataService, IMonitorService monitorService)
	{
		_device = device;
		_metadataService = metadataService;
		_monitorService = monitorService;
		_isReady = true;
		_onSettingPropertyChanged = new(OnSettingPropertyChanged);
		_lastRefreshTimestamp = Stopwatch.GetTimestamp();
	}

	public async Task UpdateInformationAsync(MonitorInformation information, CancellationToken cancellationToken)
	{
		if (information.SupportedSettings.IsDefault) return;

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
			case MonitorSetting.Sharpness:
				InitializeSetting(setting, ref _sharpnessSetting, nameof(SharpnessSetting));
				break;
			case MonitorSetting.BlueLightFilterLevel:
				InitializeSetting(setting, ref _blueLightFilterLevelSetting, nameof(BlueLightFilterLevelSetting));
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
				HasRgbSection = true;
				break;
			case MonitorSetting.VideoGainGreen:
				InitializeSetting(setting, ref _greenVideoGainSetting, nameof(GreenVideoGainSetting));
				HasRgbSection = true;
				break;
			case MonitorSetting.VideoGainBlue:
				InitializeSetting(setting, ref _blueVideoGainSetting, nameof(BlueVideoGainSetting));
				HasRgbSection = true;
				break;
			case MonitorSetting.VideoBlackLevelRed:
				InitializeSetting(setting, ref _redVideoBlackLevelSetting, nameof(RedVideoBlackLevelSetting));
				HasRgbSection = true;
				break;
			case MonitorSetting.VideoBlackLevelGreen:
				InitializeSetting(setting, ref _greenVideoBlackLevelSetting, nameof(GreenVideoBlackLevelSetting));
				HasRgbSection = true;
				break;
			case MonitorSetting.VideoBlackLevelBlue:
				InitializeSetting(setting, ref _blueVideoBlackLevelSetting, nameof(BlueVideoBlackLevelSetting));
				HasRgbSection = true;
				break;
			case MonitorSetting.SixAxisSaturationControlRed:
				InitializeSetting(setting, ref _redSixAxisSaturationControlSetting, nameof(RedSixAxisSaturationControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisSaturationControlYellow:
				InitializeSetting(setting, ref _yellowSixAxisSaturationControlSetting, nameof(YellowSixAxisSaturationControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisSaturationControlGreen:
				InitializeSetting(setting, ref _greenSixAxisSaturationControlSetting, nameof(GreenSixAxisSaturationControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisSaturationControlCyan:
				InitializeSetting(setting, ref _cyanSixAxisSaturationControlSetting, nameof(CyanSixAxisSaturationControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisSaturationControlBlue:
				InitializeSetting(setting, ref _blueSixAxisSaturationControlSetting, nameof(BlueSixAxisSaturationControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisSaturationControlMagenta:
				InitializeSetting(setting, ref _magentaSixAxisSaturationControlSetting, nameof(MagentaSixAxisSaturationControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisHueControlRed:
				InitializeSetting(setting, ref _redSixAxisHueControlSetting, nameof(RedSixAxisHueControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisHueControlYellow:
				InitializeSetting(setting, ref _yellowSixAxisHueControlSetting, nameof(YellowSixAxisHueControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisHueControlGreen:
				InitializeSetting(setting, ref _greenSixAxisHueControlSetting, nameof(GreenSixAxisHueControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisHueControlCyan:
				InitializeSetting(setting, ref _cyanSixAxisHueControlSetting, nameof(CyanSixAxisHueControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisHueControlBlue:
				InitializeSetting(setting, ref _blueSixAxisHueControlSetting, nameof(BlueSixAxisHueControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.SixAxisHueControlMagenta:
				InitializeSetting(setting, ref _magentaSixAxisHueControlSetting, nameof(MagentaSixAxisHueControlSetting));
				HasSixAxisSection = true;
				break;
			case MonitorSetting.InputLag:
				InitializeSetting(setting, ref _inputLagSetting, nameof(ResponseTimeSetting));
				await _metadataService.WaitForAvailabilityAsync(cancellationToken);
				_inputLagSetting!.UpdateNonContinuousValues(_metadataService, information.InputLagLevels);
				HasPerformanceSection = true;
				break;
			case MonitorSetting.ResponseTime:
				InitializeSetting(setting, ref _responseTimeSetting, nameof(ResponseTimeSetting));
				await _metadataService.WaitForAvailabilityAsync(cancellationToken);
				_responseTimeSetting!.UpdateNonContinuousValues(_metadataService, information.ResponseTimeLevels);
				HasPerformanceSection = true;
				break;
			case MonitorSetting.OsdLanguage:
				InitializeSetting(setting, ref _osdLanguageSetting, nameof(OsdLanguageSetting));
				await _metadataService.WaitForAvailabilityAsync(cancellationToken);
				_osdLanguageSetting!.UpdateNonContinuousValues(_metadataService, information.OsdLanguages);
				HasMiscellaneousSection = true;
				break;
			case MonitorSetting.PowerIndicator:
				InitializeSetting(setting, ref _powerIndicatorSetting, nameof(PowerIndicatorSetting));
				HasMiscellaneousSection = true;
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
		case MonitorSetting.Sharpness:
			UpdateSetting(settingValue, ref _sharpnessSetting, nameof(SharpnessSetting));
			break;
		case MonitorSetting.BlueLightFilterLevel:
			UpdateSetting(settingValue, ref _blueLightFilterLevelSetting, nameof(BlueLightFilterLevelSetting));
			break;
		case MonitorSetting.AudioVolume:
			UpdateSetting(settingValue, ref _audioVolumeSetting, nameof(AudioVolumeSetting));
			break;
		case MonitorSetting.InputSelect:
			UpdateSetting(settingValue, ref _inputSelectSetting, nameof(InputSelectSetting));
			break;
		case MonitorSetting.VideoGainRed:
			UpdateSetting(settingValue, ref _redVideoGainSetting, nameof(RedVideoGainSetting));
			HasRgbSection = true;
			break;
		case MonitorSetting.VideoGainGreen:
			UpdateSetting(settingValue, ref _greenVideoGainSetting, nameof(GreenVideoGainSetting));
			HasRgbSection = true;
			break;
		case MonitorSetting.VideoGainBlue:
			UpdateSetting(settingValue, ref _blueVideoGainSetting, nameof(BlueVideoGainSetting));
			HasRgbSection = true;
			break;
		case MonitorSetting.VideoBlackLevelRed:
			UpdateSetting(settingValue, ref _redVideoBlackLevelSetting, nameof(RedVideoBlackLevelSetting));
			HasRgbSection = true;
			break;
		case MonitorSetting.VideoBlackLevelGreen:
			UpdateSetting(settingValue, ref _greenVideoBlackLevelSetting, nameof(GreenVideoBlackLevelSetting));
			HasRgbSection = true;
			break;
		case MonitorSetting.VideoBlackLevelBlue:
			UpdateSetting(settingValue, ref _blueVideoBlackLevelSetting, nameof(BlueVideoBlackLevelSetting));
			HasRgbSection = true;
			break;
		case MonitorSetting.SixAxisSaturationControlRed:
			UpdateSetting(settingValue, ref _redSixAxisSaturationControlSetting, nameof(RedSixAxisSaturationControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisSaturationControlYellow:
			UpdateSetting(settingValue, ref _yellowSixAxisSaturationControlSetting, nameof(YellowSixAxisSaturationControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisSaturationControlGreen:
			UpdateSetting(settingValue, ref _greenSixAxisSaturationControlSetting, nameof(GreenSixAxisSaturationControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisSaturationControlCyan:
			UpdateSetting(settingValue, ref _cyanSixAxisSaturationControlSetting, nameof(CyanSixAxisSaturationControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisSaturationControlBlue:
			UpdateSetting(settingValue, ref _blueSixAxisSaturationControlSetting, nameof(BlueSixAxisSaturationControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisSaturationControlMagenta:
			UpdateSetting(settingValue, ref _magentaSixAxisSaturationControlSetting, nameof(MagentaSixAxisSaturationControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisHueControlRed:
			UpdateSetting(settingValue, ref _redSixAxisHueControlSetting, nameof(RedSixAxisHueControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisHueControlYellow:
			UpdateSetting(settingValue, ref _yellowSixAxisHueControlSetting, nameof(YellowSixAxisHueControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisHueControlGreen:
			UpdateSetting(settingValue, ref _greenSixAxisHueControlSetting, nameof(GreenSixAxisHueControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisHueControlCyan:
			UpdateSetting(settingValue, ref _cyanSixAxisHueControlSetting, nameof(CyanSixAxisHueControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisHueControlBlue:
			UpdateSetting(settingValue, ref _blueSixAxisHueControlSetting, nameof(BlueSixAxisHueControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.SixAxisHueControlMagenta:
			UpdateSetting(settingValue, ref _magentaSixAxisHueControlSetting, nameof(MagentaSixAxisHueControlSetting));
			HasSixAxisSection = true;
			break;
		case MonitorSetting.InputLag:
			UpdateSetting(settingValue, ref _inputLagSetting, nameof(InputLagSetting));
			HasPerformanceSection = true;
			break;
		case MonitorSetting.ResponseTime:
			UpdateSetting(settingValue, ref _responseTimeSetting, nameof(ResponseTimeSetting));
			HasPerformanceSection = true;
			break;
		case MonitorSetting.OsdLanguage:
			UpdateSetting(settingValue, ref _osdLanguageSetting, nameof(OsdLanguageSetting));
			HasMiscellaneousSection = true;
			break;
		case MonitorSetting.PowerIndicator:
			UpdateSetting(settingValue, ref _powerIndicatorSetting, nameof(PowerIndicatorSetting));
			HasMiscellaneousSection = true;
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
		if (!IsReady) throw new InvalidOperationException();
		IsReady = false;
		List<Exception>? exceptions = null;
		try
		{
			try { await ApplyChangeIfNeededAsync(_brightnessSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_contrastSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_sharpnessSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_blueLightFilterLevelSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_audioVolumeSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_inputSelectSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_redVideoGainSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_greenVideoGainSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_blueVideoGainSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_redVideoBlackLevelSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_greenVideoBlackLevelSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_blueVideoBlackLevelSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
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
		await setting.ApplyChangeAsync(_monitorService, _device.Id, cancellationToken);
	}

	public override bool IsChanged => _changedSettingCount != 0;

	protected override void Reset()
	{
		if (!IsReady) throw new InvalidOperationException();
		IResettable.SharedResetCommand.Execute(_brightnessSetting);
		IResettable.SharedResetCommand.Execute(_contrastSetting);
		IResettable.SharedResetCommand.Execute(_sharpnessSetting);
		IResettable.SharedResetCommand.Execute(_blueLightFilterLevelSetting);
		IResettable.SharedResetCommand.Execute(_audioVolumeSetting);
		IResettable.SharedResetCommand.Execute(_inputSelectSetting);
		IResettable.SharedResetCommand.Execute(_redVideoGainSetting);
		IResettable.SharedResetCommand.Execute(_greenVideoGainSetting);
		IResettable.SharedResetCommand.Execute(_blueVideoGainSetting);
		IResettable.SharedResetCommand.Execute(_redVideoBlackLevelSetting);
		IResettable.SharedResetCommand.Execute(_greenVideoBlackLevelSetting);
		IResettable.SharedResetCommand.Execute(_blueVideoBlackLevelSetting);
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
		IResettable.SharedResetCommand.Execute(_osdLanguageSetting);
		IResettable.SharedResetCommand.Execute(_powerIndicatorSetting);
	}

	bool IRefreshable.CanRefresh => IsReady;

	async Task IRefreshable.RefreshAsync(CancellationToken cancellationToken)
	{
		if (!IsReady) return;
		IsReady = false;
		try
		{
			await _monitorService.RefreshMonitorSettingsAsync(_device.Id, cancellationToken);
			_lastRefreshTimestamp = Stopwatch.GetTimestamp();
		}
		finally
		{
			IsReady = true;
		}
	}

	// We want to refresh the settings when the page is displayed, but because it can be quite slow in some circustances, we don't want to do it in too quick succession.
	// For example, accessing the monitors through the I2C proxy forwarding requests to the Windows DXVA2 APIs, is quite slow.
	// Generally, monitors exposing a large quantity of settings will take longer to refresh, as the basic MCCS spec has some mandatory waits of tens of ms for each call.
	public async Task DebouncedRefreshAsync(CancellationToken cancellationToken)
	{
		if (Stopwatch.GetElapsedTime(_lastRefreshTimestamp) < TimeSpan.FromSeconds(30)) return;
		await (this as IRefreshable).RefreshAsync(cancellationToken);
	}
}

internal abstract partial class MonitorDeviceSettingViewModel : ResettableBindableObject
{
	public abstract MonitorSetting Setting { get; }
	internal abstract void SetValues(ushort currentValue, ushort minimumValue, ushort maximumValue);
	internal abstract ValueTask ApplyChangeAsync(IMonitorService monitorService, Guid deviceId, CancellationToken cancellationToken);
}

internal sealed partial class ContinuousMonitorDeviceSettingViewModel : MonitorDeviceSettingViewModel
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
		MonitorSetting.VideoBlackLevelRed => "Red Video Black Level",
		MonitorSetting.VideoBlackLevelGreen => "Green Video Black Level",
		MonitorSetting.VideoBlackLevelBlue => "Blue Video Black Level",
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
		=> monitorService.SetSettingValueAsync(deviceId, Setting, Value, cancellationToken);

	protected override void Reset() => Value = InitialValue;
}

internal sealed partial class NonContinuousMonitorDeviceSettingViewModel : MonitorDeviceSettingViewModel
{
	private ushort _value;
	private ushort _initialValue;
	private readonly ObservableCollection<NonContinuousValueViewModel> _supportedValueCollection;
	private readonly ReadOnlyObservableCollection<NonContinuousValueViewModel> _readOnlySupportedValueCollection;
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

	public ReadOnlyObservableCollection<NonContinuousValueViewModel> SupportedValues => _readOnlySupportedValueCollection;

	public NonContinuousMonitorDeviceSettingViewModel(MonitorSetting setting, ushort currentValue)
	{
		Setting = setting;
		_supportedValueCollection = new();
		_readOnlySupportedValueCollection = new(_supportedValueCollection);
		_supportedValues = new();
		_value = _initialValue = currentValue;
	}

	internal void UpdateNonContinuousValues(ISettingsMetadataService metadataService, ImmutableArray<NonContinuousValueDescription> values)
	{
		if (values.IsDefaultOrEmpty)
		{
			_supportedValues.Clear();
			_supportedValueCollection.Clear();
			return;
		}

		// This is a more direct shortcut than the generic code below, for the main use case which is to initialize the collection.
		// We do not strictly need this, but if there were many lists to initialize, the few extra operations could become noticeable. (I guess)
		if (_supportedValueCollection.Count == 0 && _supportedValues.Count == 0)
		{
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
				var vm = new NonContinuousValueViewModel(this, valueDefinition.Value, friendlyName);
				_supportedValues.Add(valueDefinition.Value, vm);
				_supportedValueCollection.Add(vm);
			}
			return;
		}

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

		// Implement a diff algorithm similar in essence to the one used for custom menu management in the service.
		// I should probably find a way to make this generic so that it can be reused in all places where a similar logic is needed.
		if (_supportedValueCollection.Count != values.Length || _supportedValues.Count > values.Length)
		{
			var oldInitialValue = InitialValue;
			var oldValue = Value;

			var newValues = new Dictionary<ushort, int>();
			for (int i = 0; i < values.Length; i++)
			{
				newValues.Add(values[i].Value, i);
			}

			int runningIndex = 0;
			List<int>? removedIndices = null;
			List<int>? updatedIndices = null;
			for (int i = 0; i < _supportedValueCollection.Count; i++)
			{
				if (!newValues.Remove(_supportedValueCollection[i].Value, out var newItemPosition))
				{
					// We could remove the items immediately, but from an algorithmic POV, it is always more efficient to only remove items from the end, in reverse order.
					(removedIndices ??= []).Add(i);
				}
				else
				{
					if (runningIndex != newItemPosition) (updatedIndices ??= []).Add(newItemPosition);
					runningIndex++;
				}
			}

			foreach (var addedIndex in newValues.Values)
			{
				(updatedIndices ??= []).Add(addedIndex);
			}

			// Remove items from the end.
			// TODO: Backport this to the custom menu logic?
			if (removedIndices is { Count: > 0 })
			{
				var removedIndicesSpan = CollectionsMarshal.AsSpan(removedIndices);
				for (int i = removedIndicesSpan.Length; --i >= 0;)
				{
					_supportedValueCollection.RemoveAt(removedIndicesSpan[i]);
				}
			}

			if (updatedIndices is { Count: > 0 })
			{
				var updatedIndicesSpan = CollectionsMarshal.AsSpan(updatedIndices);
				updatedIndicesSpan.Sort();
				for (int i = 0; i < updatedIndicesSpan.Length; i++)
				{
					int index = updatedIndicesSpan[i];
					ushort value = values[index].Value;
					var vm = _supportedValues[value];
					if (newValues.ContainsKey(values[index].Value)) _supportedValueCollection.Insert(index, vm);
					else _supportedValueCollection.Move(_supportedValueCollection.IndexOf(vm), index);
				}
			}

			// TODO
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
			monitorService.SetSettingValueAsync(deviceId, Setting, Value.Value, cancellationToken) :
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

internal sealed partial class NonContinuousValueViewModel : BindableObject
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

internal sealed partial class BooleanMonitorDeviceSettingViewModel : MonitorDeviceSettingViewModel
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
		=> monitorService.SetSettingValueAsync(deviceId, Setting, Value ? (ushort)1 : (ushort)0, cancellationToken);

	protected override void Reset() => Value = InitialValue;
}
