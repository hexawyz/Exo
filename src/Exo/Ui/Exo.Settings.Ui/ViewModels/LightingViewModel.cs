using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Exo.Lighting;
using Exo.Metadata;
using Exo.Service;
using Exo.Settings.Ui.Services;
using Exo.Ui;
using Microsoft.Extensions.Logging;
using ILightingService = Exo.Settings.Ui.Services.ILightingService;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingViewModel : BindableObject, IAsyncDisposable
{
	private readonly DevicesViewModel _devicesViewModel;
	private readonly ISettingsMetadataService _metadataService;
	private ILightingService? _lightingService;
	private readonly ObservableCollection<LightingDeviceViewModel> _lightingDevices;
	private readonly Dictionary<Guid, LightingDeviceViewModel> _lightingDeviceById;
	private readonly ConcurrentDictionary<Guid, LightingEffectViewModel> _effectViewModelById;
	private readonly Dictionary<Guid, LightingDeviceConfiguration> _pendingConfigurationUpdates;
	private readonly Dictionary<Guid, LightingDeviceInformation> _pendingDeviceInformations;
	private readonly ILogger<LightingDeviceViewModel> _lightingDeviceLogger;
	private readonly INotificationSystem _notificationSystem;

	private readonly CancellationTokenSource _cancellationTokenSource;

	public ObservableCollection<LightingDeviceViewModel> LightingDevices => _lightingDevices;
	public ILightingService? LightingService => _lightingService;

	public LightingViewModel(ITypedLoggerProvider loggerProvider, DevicesViewModel devicesViewModel, ISettingsMetadataService metadataService, INotificationSystem notificationSystem)
	{
		_lightingDeviceLogger = loggerProvider.GetLogger<LightingDeviceViewModel>();
		_notificationSystem = notificationSystem;
		_devicesViewModel = devicesViewModel;
		_metadataService = metadataService;
		_lightingDevices = new();
		_lightingDeviceById = new();
		_effectViewModelById = new();
		_pendingConfigurationUpdates = new();
		_pendingDeviceInformations = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_devicesViewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
	}

	internal void OnConnected(ILightingService lightingService)
	{
		_lightingService = lightingService;
	}

	internal void OnConnectionReset()
	{
		_lightingDeviceById.Clear();
		_effectViewModelById.Clear();
		_pendingConfigurationUpdates.Clear();
		_pendingDeviceInformations.Clear();

		foreach (var device in _lightingDevices)
		{
			device.Dispose();
		}

		_lightingDevices.Clear();

		_lightingService = null;
	}

	public ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		return ValueTask.CompletedTask;
	}

	private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			var vm = (DeviceViewModel)e.NewItems![0]!;
			if (_pendingDeviceInformations.Remove(vm.Id, out var info))
			{
				OnDeviceAdded(vm, info);
			}
		}
		else if (e.Action == NotifyCollectionChangedAction.Remove)
		{
			var vm = (DeviceViewModel)e.OldItems![0]!;
			if (!_pendingDeviceInformations.Remove(vm.Id))
			{
				OnDeviceRemoved(vm.Id);
			}
		}
		else if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			// Reset will only be triggered when the service connection is reset. In that case, the change will be handled in the appropriate reset code for this component.
		}
		else
		{
			// As of writing this code, we don't require support for anything else, but if this change in the future, this exception will be triggered.
			throw new InvalidOperationException("This case is not handled.");
		}
	}

	private void OnDeviceAdded(DeviceViewModel device, LightingDeviceInformation lightingDeviceInformation)
	{
		var vm = new LightingDeviceViewModel(_lightingDeviceLogger, this, device, lightingDeviceInformation, _notificationSystem);
		_lightingDevices.Add(vm);
		_lightingDeviceById[vm.Id] = vm;
		if (_pendingConfigurationUpdates.Remove(lightingDeviceInformation.DeviceId, out var configuration))
		{
			vm.OnDeviceConfigurationUpdated(in configuration);
		}
	}

	private void OnDeviceRemoved(Guid deviceId)
	{
		for (int i = 0; i < _lightingDevices.Count; i++)
		{
			var vm = _lightingDevices[i];
			if (_lightingDevices[i].Id == deviceId)
			{
				_lightingDevices.RemoveAt(i);
				_lightingDeviceById.Remove(vm.Id);
				break;
			}
		}
	}

	internal void OnLightingDevice(LightingDeviceInformation info)
	{
		if (_lightingDeviceById.TryGetValue(info.DeviceId, out var vm))
		{
			// TODO: Update lighting zones ?
		}
		else
		{
			if (_devicesViewModel.TryGetDevice(info.DeviceId, out var device))
			{
				OnDeviceAdded(device, info);
			}
			else if (!_devicesViewModel.IsRemovedId(info.DeviceId))
			{
				_pendingDeviceInformations[info.DeviceId] = info;
			}
		}
	}

	internal void OnLightingConfigurationUpdate(in LightingDeviceConfiguration configuration)
	{
		if (_lightingDeviceById.TryGetValue(configuration.DeviceId, out var vm))
		{
			vm.OnDeviceConfigurationUpdated(in configuration);
		}
		else
		{
			_pendingConfigurationUpdates[configuration.DeviceId] = configuration;
		}
	}

	internal void CacheEffectInformation(LightingEffectInformation effectInformation)
	{
		if (_effectViewModelById.TryGetValue(effectInformation.EffectId, out var vm))
		{
			// NB: This is imperfect. If an effect is currently selected, it won't recreate the properties.
			vm.OnMetadataUpdated(effectInformation);
		}
		else
		{
			string? displayName = null;
			uint displayOrder = uint.MaxValue;
			if (_metadataService.TryGetLightingEffectMetadata("", "", effectInformation.EffectId, out var metadata))
			{
				displayName = _metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
				displayOrder = metadata.DisplayOrder;
			}
			displayName ??= string.Create(CultureInfo.InvariantCulture, $"Effect {effectInformation.EffectId:B}.");

			_effectViewModelById.TryAdd(effectInformation.EffectId, new(effectInformation, displayName, displayOrder));
		}
	}

	public LightingEffectViewModel GetEffect(Guid effectId)
		=> _effectViewModelById.TryGetValue(effectId, out var effect) ? effect : throw new InvalidOperationException("Missing effect information.");

	public (string DisplayName, int DisplayOrder, LightingZoneComponentType ComponentType, LightingZoneShape Shape) GetZoneMetadata(Guid zoneId)
	{
		string? displayName = null;
		int displayOrder = 0;
		LightingZoneComponentType componentType = 0;
		LightingZoneShape shape = 0;
		if (_metadataService.TryGetLightingZoneMetadata("", "", zoneId, out var metadata))
		{
			displayName = _metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
			displayOrder = metadata.DisplayOrder;
			componentType = metadata.ComponentType;
			shape = metadata.Shape;
		}
		return (displayName ?? $"Unknown {zoneId:B}", displayOrder, componentType, shape);
	}
}
