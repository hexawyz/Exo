using Exo.Service;
using Exo.Settings.Ui.Services;
using Microsoft.Extensions.Logging;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class PowerFeaturesViewModel : ApplicableResettableBindableObject
{
	private readonly DeviceViewModel _device;
	private readonly IPowerService _powerService;
	private BatteryStateViewModel? _batteryState;
	private TimeSpan _minimumIdleSleepDelay;
	private TimeSpan _maximumIdleSleepDelay;
	private TimeSpan _initialIdleSleepDelay;
	private TimeSpan _currentIdleSleepDelay;
	private PowerDeviceFlags _capabilities;
	private Half _initialLowPowerModeBatteryThreshold;
	private Half _currentLowPowerModeBatteryThreshold;
	private byte _minimumBrightness;
	private byte _maximumBrightness;
	private byte _initialWirelessBrightness;
	private byte _currentWirelessBrightness;
	private bool _isExpanded;
	private readonly ILogger<PowerFeaturesViewModel> _logger;

	public PowerFeaturesViewModel(ITypedLoggerProvider loggerProvider, DeviceViewModel device, IPowerService powerService)
	{
		_logger = loggerProvider.GetLogger<PowerFeaturesViewModel>();
		_device = device;
		_powerService = powerService;
	}

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	public override bool IsChanged
		=> _currentIdleSleepDelay != _initialIdleSleepDelay ||
			_initialLowPowerModeBatteryThreshold != _currentLowPowerModeBatteryThreshold ||
			_initialWirelessBrightness != _currentWirelessBrightness;

	public bool HasLowPowerBatteryThreshold => (_capabilities & PowerDeviceFlags.HasLowPowerBatteryThreshold) != 0;
	public bool HasIdleTimer => (_capabilities & PowerDeviceFlags.HasIdleTimer) != 0;
	public bool HasWirelessBrightness => (_capabilities & PowerDeviceFlags.HasWirelessBrightness) != 0;

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

	public byte MinimumBrightness => _minimumBrightness;
	public byte MaximumBrightness => _maximumBrightness;

	public byte WirelessBrightness
	{
		get => _currentWirelessBrightness;
		set => SetChangeableValue(ref _currentWirelessBrightness, value, ChangedProperty.WirelessBrightness);
	}

	public void UpdateInformation(PowerDeviceInformation information)
	{
		var oldMinimumIdleSleepDelay = _minimumIdleSleepDelay;
		var oldMaximumIdleSleepDelay = _maximumIdleSleepDelay;
		bool hadLowPowerBatteryThreshold = HasLowPowerBatteryThreshold;
		bool hadIdleTimer = HasIdleTimer;
		bool hadWirelessBrightness = HasWirelessBrightness;
		bool wasChanged = IsChanged;
		_capabilities = information.Flags;
		if ((_capabilities & PowerDeviceFlags.HasBattery) == 0)
		{
			_batteryState = null;
		}
		if ((_capabilities & PowerDeviceFlags.HasIdleTimer) != 0)
		{
			_minimumIdleSleepDelay = information.MinimumIdleTime;
			_maximumIdleSleepDelay = information.MaximumIdleTime;
		}
		else
		{
			_maximumIdleSleepDelay = _minimumIdleSleepDelay = default;
			IdleSleepDelay = _initialIdleSleepDelay;
		}
		if ((_capabilities & PowerDeviceFlags.HasLowPowerBatteryThreshold) == 0)
		{
			LowPowerModeBatteryThreshold = _initialLowPowerModeBatteryThreshold;
		}
		if ((_capabilities & PowerDeviceFlags.HasWirelessBrightness) != 0)
		{
			_minimumBrightness = information.MinimumBrightness;
			_maximumBrightness = information.MaximumBrightness;
		}
		else
		{
			_minimumBrightness = _maximumBrightness = 0;
			WirelessBrightness = _initialWirelessBrightness;
		}
		if (oldMinimumIdleSleepDelay != _minimumIdleSleepDelay) NotifyPropertyChanged(ChangedProperty.MinimumIdleSleepDelay);
		if (oldMaximumIdleSleepDelay != _maximumIdleSleepDelay) NotifyPropertyChanged(ChangedProperty.MaximumIdleSleepDelay);
		if (hadLowPowerBatteryThreshold != HasLowPowerBatteryThreshold) NotifyPropertyChanged(ChangedProperty.HasLowPowerBatteryThreshold);
		if (hadIdleTimer != HasIdleTimer) NotifyPropertyChanged(ChangedProperty.HasIdleTimer);
		if (hadWirelessBrightness != HasWirelessBrightness) NotifyPropertyChanged(ChangedProperty.HasWirelessBrightness);
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

	public void UpdateWirelessBrightness(byte brightness)
	{
		if (brightness == _initialWirelessBrightness) return;

		bool wasChanged = IsChanged;
		if (_initialWirelessBrightness == _currentWirelessBrightness)
		{
			_currentWirelessBrightness = brightness;
			NotifyPropertyChanged(ChangedProperty.WirelessBrightness);
		}
		_initialWirelessBrightness = brightness;
		OnChangeStateChange(wasChanged);
	}

	protected override void Reset()
	{
		IdleSleepDelay = _initialIdleSleepDelay;
		LowPowerModeBatteryThreshold = _initialLowPowerModeBatteryThreshold;
		WirelessBrightness = _initialWirelessBrightness;
	}

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		if (HasLowPowerBatteryThreshold && _currentLowPowerModeBatteryThreshold != _initialLowPowerModeBatteryThreshold)
		{
			try
			{
				await _powerService.SetLowPowerModeBatteryThresholdAsync(_device.Id, _currentLowPowerModeBatteryThreshold, cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.PowerLowPowerModeBatteryThresholdError(_device.FriendlyName, ex);
			}
		}
		if (HasIdleTimer && _currentIdleSleepDelay != _initialIdleSleepDelay)
		{
			try
			{
				await _powerService.SetIdleSleepTimerAsync(_device.Id, _currentIdleSleepDelay, cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.PowerIdleSleepTimerError(_device.FriendlyName, ex);
			}
		}
		if (HasWirelessBrightness && _currentWirelessBrightness != _initialWirelessBrightness)
		{
			try
			{
				await _powerService.SetWirelessBrightnessAsync(_device.Id, _currentWirelessBrightness, cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.PowerWirelessBrightnessError(_device.FriendlyName, ex);
			}
		}
	}
}
