using System.Collections.ObjectModel;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightDeviceFeaturesViewModel : BindableObject, IDisposable
{
	private readonly DeviceViewModel _device;
	private readonly ISettingsMetadataService _metadataService;
	private readonly ILightService _lightService;
	private readonly ObservableCollection<LightViewModel> _lights;
	private readonly ReadOnlyObservableCollection<LightViewModel> _readOnlyLights;
	private readonly Dictionary<Guid, LightViewModel> _lightById;
	private readonly Dictionary<Guid, LightChangeNotification> _pendingLightChanges;
	private bool _isExpanded;
	private readonly INotificationSystem _notificationSystem;

	public LightDeviceFeaturesViewModel
	(
		DeviceViewModel device,
		ISettingsMetadataService metadataService,
		ILightService lightService,
		INotificationSystem notificationSystem
	)
	{
		_device = device;
		_metadataService = metadataService;
		_lightService = lightService;
		_notificationSystem = notificationSystem;
		_lights = new();
		_lightById = new();
		_pendingLightChanges = new();
		_readOnlyLights = new(_lights);
	}

	public void Dispose() { }

	public Guid DeviceId => _device.Id;

	public ReadOnlyObservableCollection<LightViewModel> Lights => _readOnlyLights;

	internal DeviceViewModel Device => _device;

	internal ISettingsMetadataService MetadataService => _metadataService;

	internal INotificationSystem NotificationSystem => _notificationSystem;

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	internal ILightService LightService => _lightService;

	internal void UpdateInformation(LightDeviceInformation information)
	{
		var lightIds = new HashSet<Guid>();
		foreach (var monitorInformation in information.Lights)
		{
			lightIds.Add(monitorInformation.LightId);
		}
		for (int i = 0; i < _lights.Count; i++)
		{
			var embeddedMonitor = _lights[i];
			if (!lightIds.Contains(embeddedMonitor.LightId))
			{
				_lights.RemoveAt(i--);
				_lightById.Remove(embeddedMonitor.LightId);
			}
		}
		foreach (var lightInformation in information.Lights)
		{
			if (_lightById.TryGetValue(lightInformation.LightId, out var vm))
			{
				vm.UpdateInformation(lightInformation);
			}
			else
			{
				vm = new(this, lightInformation);
				_lightById.Add(lightInformation.LightId, vm);
				_lights.Add(vm);
			}
			if (_pendingLightChanges.Remove(lightInformation.LightId, out var notification))
			{
				vm.UpdateState(notification);
			}
		}
	}

	internal void UpdateLightState(LightChangeNotification configuration)
	{
		if (_lightById.TryGetValue(configuration.LightId, out var light))
		{
			light.UpdateState(configuration);
		}
		else
		{
			_pendingLightChanges.Add(configuration.LightId, configuration);
		}
	}
}

internal sealed class LightViewModel : BindableObject
{
	private readonly LightDeviceFeaturesViewModel _owner;
	private readonly Guid _lightId;
	private LightCapabilities _capabilities;
	private byte _minimumBrightness;
	private byte _maximumBrightness;
	private uint _minimumTemperature;
	private uint _maximumTemperature;
	private bool _isOn;
	private bool _liveIsOn;
	private byte _brightness;
	private byte _liveBrightness;
	private uint _temperature;
	private uint _liveTemperature;

	public LightViewModel(LightDeviceFeaturesViewModel owner, LightInformation information)
	{
		_owner = owner;
		_lightId = information.LightId;
		_capabilities = information.Capabilities;
		_minimumBrightness = information.MinimumBrightness;
		_maximumBrightness = information.MaximumBrightness;
		_minimumTemperature = information.MinimumTemperature;
		_maximumTemperature = information.MaximumTemperature;
	}

	public Guid LightId => _lightId;

	public string DisplayName => _owner.Device.FriendlyName;

	public bool IsOn
	{
		get => _liveIsOn;
		set
		{
			if (value != _liveIsOn)
			{
				_liveIsOn = value;
				NotifyPropertyChanged(ChangedProperty.IsOn);
				Switch(value);
			}
		}
	}

	public byte Brightness
	{
		get => _liveBrightness;
		set
		{
			if (value != _liveBrightness)
			{
				_liveBrightness = value;
				NotifyPropertyChanged(ChangedProperty.Brightness);
				SetBrightness(value);
			}
		}
	}

	public uint Temperature
	{
		get => _liveTemperature;
		set
		{
			if (value != _liveTemperature)
			{
				_liveTemperature = value;
				NotifyPropertyChanged(ChangedProperty.Temperature);
				SetTemperature(value);
			}
		}
	}

	private LightCapabilities Capabilities
	{
		get => _capabilities;
		set
		{
			if (value != _capabilities)
			{
				var changedCapabilities = value ^ _capabilities;
				_capabilities = value;
				if ((changedCapabilities & LightCapabilities.Brightness) != 0) NotifyPropertyChanged(ChangedProperty.HasBrightness);
				if ((changedCapabilities & LightCapabilities.Temperature) != 0) NotifyPropertyChanged(ChangedProperty.HasTemperature);
			}
		}
	}

	public bool HasBrightness => (_capabilities & LightCapabilities.Brightness) != 0;
	public bool HasTemperature => (_capabilities & LightCapabilities.Temperature) != 0;

	public byte MinimumBrightness
	{
		get => _minimumBrightness;
		set => SetValue(ref _minimumBrightness, value, ChangedProperty.MinimumBrightness);
	}

	public byte MaximumBrightness
	{
		get => _maximumBrightness;
		set => SetValue(ref _maximumBrightness, value, ChangedProperty.MaximumBrightness);
	}

	public uint MinimumTemperature
	{
		get => _minimumTemperature;
		set => SetValue(ref _minimumTemperature, value, ChangedProperty.MinimumTemperature);
	}

	public uint MaximumTemperature
	{
		get => _maximumTemperature;
		set => SetValue(ref _maximumTemperature, value, ChangedProperty.MaximumTemperature);
	}

	internal void UpdateInformation(LightInformation information)
	{
		Capabilities = information.Capabilities;
		MinimumBrightness = information.MinimumBrightness;
		MaximumBrightness = information.MaximumBrightness;
		MinimumTemperature = information.MinimumTemperature;
		MaximumTemperature = information.MaximumTemperature;
	}

	internal void UpdateState(LightChangeNotification notification)
	{
		bool isOnChanged = false;
		bool brightnessChanged = false;
		bool temperatureChanged = false;
		if (notification.IsOn != _isOn)
		{
			if (isOnChanged = _liveIsOn == _isOn)
			{
				_liveIsOn = notification.IsOn;
			}
			_isOn = notification.IsOn;
		}
		if (notification.Brightness != _brightness)
		{
			if (brightnessChanged = _liveBrightness == _brightness)
			{
				_liveBrightness = notification.Brightness;
			}
			_brightness = notification.Brightness;
		}
		if (notification.Temperature != _temperature)
		{
			if (temperatureChanged = _liveTemperature == _temperature)
			{
				_liveTemperature = notification.Temperature;
			}
			_temperature = notification.Temperature;
		}
		if (isOnChanged) NotifyPropertyChanged(ChangedProperty.IsOn);
		if (brightnessChanged) NotifyPropertyChanged(ChangedProperty.Brightness);
		if (temperatureChanged) NotifyPropertyChanged(ChangedProperty.Temperature);
	}

	private async void Switch(bool isOn)
	{
		try
		{
			await SwitchAsync(isOn, default);
		}
		catch
		{
		}
	}

	// TODO: Probably need to have some kind of queue for updates
	private async Task SwitchAsync(bool isOn, CancellationToken cancellationToken)
	{
		await _owner.LightService.SwitchLightAsync(new() { DeviceId = _owner.DeviceId, LightId = _lightId, IsOn = isOn }, cancellationToken);
	}

	private async void SetBrightness(byte brightness)
	{
		try
		{
			await SetBrightnessAsync(brightness, default);
		}
		catch
		{
		}
	}

	private async Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
	{
		//await _owner.LightService.SwitchLightAsync(new() { DeviceId = _owner.DeviceId, LightId = _lightId, IsOn = isOn }, cancellationToken);
	}

	private async void SetTemperature(uint temperature)
	{
		try
		{
			await SetTemperatureAsync(temperature, default);
		}
		catch
		{
		}
	}

	private async Task SetTemperatureAsync(uint temperature, CancellationToken cancellationToken)
	{
		//await _owner.LightService.SwitchLightAsync(new() { DeviceId = _owner.DeviceId, LightId = _lightId, IsOn = isOn }, cancellationToken);
	}
}
