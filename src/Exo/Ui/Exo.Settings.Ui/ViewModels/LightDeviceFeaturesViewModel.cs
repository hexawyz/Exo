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
	private bool _isOn;
	private bool _liveIsOn;

	public LightViewModel(LightDeviceFeaturesViewModel owner, LightInformation information)
	{
		_owner = owner;
		_lightId = information.LightId;
		_capabilities = information.Capabilities;
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

	internal void UpdateInformation(LightInformation information)
	{
		Capabilities = information.Capabilities;
	}

	internal void UpdateState(LightChangeNotification notification)
	{
		bool isOnChanged = false;
		if (notification.IsOn != _isOn)
		{
			if (isOnChanged = _liveIsOn == _isOn)
			{
				_liveIsOn = notification.IsOn;
			}
			_isOn = notification.IsOn;
		}
		if (isOnChanged) NotifyPropertyChanged(ChangedProperty.IsOn);
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
}
