using Exo.Contracts;
using Exo.Contracts.Ui.Settings;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class PowerFeaturesViewModel : ApplicableResettableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly IPowerService _powerService;
	private BatteryStateViewModel? _batteryState;
	private TimeSpan _minimumIdleSleepDelay;
	private TimeSpan _maximumIdleSleepDelay;
	private TimeSpan _initialIdleSleepDelay;
	private TimeSpan _currentIdleSleepDelay;
	private PowerDeviceCapabilities _capabilities;
	private Half _initialLowPowerModeBatteryThreshold;
	private Half _currentLowPowerModeBatteryThreshold;
	private bool _isExpanded;

	public PowerFeaturesViewModel(DeviceViewModel device, IPowerService powerService)
	{
		_device = device;
		_powerService = powerService;
	}

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	public override bool IsChanged => _currentIdleSleepDelay != _initialIdleSleepDelay || _initialLowPowerModeBatteryThreshold != _currentLowPowerModeBatteryThreshold;
	public bool HasLowPowerBatteryThreshold => (_capabilities & PowerDeviceCapabilities.HasLowPowerBatteryThreshold) != 0;
	public bool HasIdleTimer => (_capabilities & PowerDeviceCapabilities.HasIdleTimer) != 0;

	public DeviceViewModel Device => _device;

	public BatteryStateViewModel? BatteryState
	{
		get => _batteryState;
		set => SetValue(ref _batteryState, value, ChangedProperty.BatteryState);
	}

	public TimeSpan MinimumIdleSleepDelay => _minimumIdleSleepDelay;
	public TimeSpan MaximumIdleSleepDelay => _maximumIdleSleepDelay;

	public TimeSpan IdleSleepDelay
	{
		get => _currentIdleSleepDelay;
		set => SetChangeableValue(ref _currentIdleSleepDelay, value, ChangedProperty.IdleSleepDelay);
	}

	public Half LowPowerModeBatteryThreshold
	{
		get => _currentLowPowerModeBatteryThreshold;
		set => SetChangeableValue(ref _currentLowPowerModeBatteryThreshold, value, ChangedProperty.LowPowerModeBatteryThreshold);
	}

	public void UpdateInformation(PowerDeviceInformation information)
	{
		var oldMinimumIdleSleepDelay = _minimumIdleSleepDelay;
		var oldMaximumIdleSleepDelay = _maximumIdleSleepDelay;
		bool hadLowPowerBatteryThreshold = HasLowPowerBatteryThreshold;
		bool hadIdleTimer = HasIdleTimer;
		bool wasChanged = IsChanged;
		_capabilities = information.Capabilities;
		if ((_capabilities & PowerDeviceCapabilities.HasBattery) == 0)
		{
			_batteryState = null;
		}
		if ((_capabilities & PowerDeviceCapabilities.HasIdleTimer) != 0)
		{
			_minimumIdleSleepDelay = information.MinimumIdleTime;
			_maximumIdleSleepDelay = information.MaximumIdleTime;
		}
		else
		{
			_currentIdleSleepDelay = _initialIdleSleepDelay = _maximumIdleSleepDelay = _minimumIdleSleepDelay = default;
		}
		if ((_capabilities & PowerDeviceCapabilities.HasLowPowerBatteryThreshold) == 0)
		{
			_currentLowPowerModeBatteryThreshold = _initialLowPowerModeBatteryThreshold;
		}
		if (oldMinimumIdleSleepDelay != _minimumIdleSleepDelay) NotifyPropertyChanged(ChangedProperty.MinimumIdleSleepDelay);
		if (oldMaximumIdleSleepDelay != _maximumIdleSleepDelay) NotifyPropertyChanged(ChangedProperty.MaximumIdleSleepDelay);
		if (hadLowPowerBatteryThreshold != HasLowPowerBatteryThreshold) NotifyPropertyChanged(ChangedProperty.HasLowPowerBatteryThreshold);
		if (hadIdleTimer != HasIdleTimer) NotifyPropertyChanged(ChangedProperty.HasIdleTimer);
		OnChangeStateChange(wasChanged);
	}

	public void UpdateIdleSleepTimer(TimeSpan idleSleepDelay)
	{
		if (idleSleepDelay == _initialIdleSleepDelay) return;

		bool wasChanged = IsChanged;
		if (_initialIdleSleepDelay == _currentIdleSleepDelay)
		{
			_currentIdleSleepDelay = idleSleepDelay;
			NotifyPropertyChanged(ChangedProperty.IdleSleepDelay);
		}
		_initialIdleSleepDelay = idleSleepDelay;
		OnChangeStateChange(wasChanged);
	}

	public void UpdateLowPowerModeBatteryThreshold(Half batteryThreshold)
	{
		if (batteryThreshold == _initialLowPowerModeBatteryThreshold) return;

		bool wasChanged = IsChanged;
		if (_initialLowPowerModeBatteryThreshold == _currentLowPowerModeBatteryThreshold)
		{
			_currentLowPowerModeBatteryThreshold = batteryThreshold;
			NotifyPropertyChanged(ChangedProperty.LowPowerModeBatteryThreshold);
		}
		_initialLowPowerModeBatteryThreshold = batteryThreshold;
		OnChangeStateChange(wasChanged);
	}

	protected override void Reset()
	{
		IdleSleepDelay = _initialIdleSleepDelay;
		LowPowerModeBatteryThreshold = _initialLowPowerModeBatteryThreshold;
	}

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (HasLowPowerBatteryThreshold && _currentLowPowerModeBatteryThreshold != _initialLowPowerModeBatteryThreshold)
		{
			await _powerService.SetLowPowerModeBatteryThresholdAsync(new() { DeviceId = _device.Id, BatteryThreshold = _currentLowPowerModeBatteryThreshold }, cancellationToken);
		}
		if (HasIdleTimer && _currentIdleSleepDelay != _initialIdleSleepDelay)
		{
			await _powerService.SetIdleSleepTimerAsync(new() { DeviceId = _device.Id, IdleTime = _currentIdleSleepDelay }, cancellationToken);
		}
	}
}
