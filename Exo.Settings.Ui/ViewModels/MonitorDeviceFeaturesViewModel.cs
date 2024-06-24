using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class MonitorDeviceFeaturesViewModel : ResettableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly ISettingsMetadataService _metadataService;
	private readonly SettingsServiceConnectionManager _connectionManager;
	private ContinuousMonitorDeviceSettingViewModel? _brightnessSetting;
	private ContinuousMonitorDeviceSettingViewModel? _contrastSetting;
	private ContinuousMonitorDeviceSettingViewModel? _audioVolumeSetting;
	private NonContinuousMonitorDeviceSettingViewModel? _inputSelectSetting;
	private readonly PropertyChangedEventHandler _onSettingPropertyChanged;

	private int _changedSettingCount;
	private bool _isReady;

	public ContinuousMonitorDeviceSettingViewModel? BrightnessSetting => _brightnessSetting;
	public ContinuousMonitorDeviceSettingViewModel? ContrastSetting => _contrastSetting;
	public ContinuousMonitorDeviceSettingViewModel? AudioVolumeSetting => _audioVolumeSetting;
	public NonContinuousMonitorDeviceSettingViewModel? InputSelectSetting => _inputSelectSetting;

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

	public async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		IsReady = false;
		List<Exception>? exceptions = null;
		try
		{
			try { await ApplyChangeIfNeededAsync(_brightnessSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_contrastSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_audioVolumeSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }
			try { await ApplyChangeIfNeededAsync(_inputSelectSetting, cancellationToken); } catch (Exception ex) { (exceptions ??= []).Add(ex); }

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
		_brightnessSetting?.Reset();
		_contrastSetting?.Reset();
		_audioVolumeSetting?.Reset();
		_inputSelectSetting?.Reset();
	}
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
		private set
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
				friendlyName = metadataService.GetString(CultureInfo.InvariantCulture, stringId);
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
			monitorService.SetInputSourceAsync(new MonitorSettingDirectUpdate { DeviceId = deviceId, Value = Value.Value }, cancellationToken) :
			ValueTask.CompletedTask;

	public override void Reset() => Value = InitialValue;
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
