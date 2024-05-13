using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class CoolingViewModel : IAsyncDisposable, IConnectedState
{
	private readonly SettingsServiceConnectionManager _connectionManager;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly SensorsViewModel _sensorsViewModel;
	private readonly ObservableCollection<CoolingDeviceViewModel> _coolingDevices;
	private readonly Dictionary<Guid, CoolingDeviceViewModel> _coolingDevicesById;
	private readonly Dictionary<Guid, CoolingDeviceInformation> _pendingDeviceInformations;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public ObservableCollection<CoolingDeviceViewModel> Devices => _coolingDevices;

	public CoolingViewModel(SettingsServiceConnectionManager connectionManager, DevicesViewModel devicesViewModel, SensorsViewModel sensorsViewModel)
	{
		_connectionManager = connectionManager;
		_devicesViewModel = devicesViewModel;
		_sensorsViewModel = sensorsViewModel;
		_coolingDevices = new();
		_coolingDevicesById = new();
		_pendingDeviceInformations = new();
		_cancellationTokenSource = new();
		_devicesViewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
		_sensorsViewModel.Devices.CollectionChanged += OnSensorDevicesCollectionChanged;
		_stateRegistration = _connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		_stateRegistration.Dispose();
		return ValueTask.CompletedTask;
	}

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource.IsCancellationRequested) return;
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			await WatchDevicesAsync(cts.Token).ConfigureAwait(false);
		}
	}

	void IConnectedState.Reset()
	{
		_coolingDevicesById.Clear();
		_pendingDeviceInformations.Clear();

		foreach (var device in _coolingDevices)
		{
			device.Dispose();
		}

		_coolingDevices.Clear();
	}

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var coolerService = await _connectionManager.GetCoolingServiceAsync(cancellationToken);
			await foreach (var info in coolerService.WatchCoolingDevicesAsync(cancellationToken))
			{
				if (_coolingDevicesById.TryGetValue(info.DeviceId, out var vm))
				{
					OnDeviceChanged(vm, info);
				}
				else
				{
					if (_devicesViewModel.TryGetDevice(info.DeviceId, out var device))
					{
						OnDeviceAdded(device, info);
					}
					else if (!_devicesViewModel.IsRemovedId(info.DeviceId))
					{
						_pendingDeviceInformations.Add(info.DeviceId, info);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
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

	private void OnDeviceAdded(DeviceViewModel device, CoolingDeviceInformation coolingDeviceInformation)
	{
		var vm = new CoolingDeviceViewModel(this, device, _sensorsViewModel.GetDevice(device.Id), coolingDeviceInformation);
		_coolingDevices.Add(vm);
		_coolingDevicesById[vm.Id] = vm;
	}

	private void OnDeviceChanged(CoolingDeviceViewModel viewModel, CoolingDeviceInformation coolingDeviceInformation)
	{
		viewModel.UpdateDeviceInformation(coolingDeviceInformation);
	}

	private void OnDeviceRemoved(Guid deviceId)
	{
		for (int i = 0; i < _coolingDevices.Count; i++)
		{
			var vm = _coolingDevices[i];
			if (_coolingDevices[i].Id == deviceId)
			{
				_coolingDevices.RemoveAt(i);
				_coolingDevicesById.Remove(vm.Id);
				break;
			}
		}
	}

	private void OnSensorDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			var sensorDevice = (SensorDeviceViewModel)e.NewItems![0]!;
			if (_coolingDevicesById.TryGetValue(sensorDevice.Id, out var coolingDevice))
			{
				coolingDevice.BindSensors(sensorDevice);
			}
		}
		else if (e.Action == NotifyCollectionChangedAction.Remove)
		{
			var sensorDevice = (SensorDeviceViewModel)e.OldItems![0]!;
			if (_coolingDevicesById.TryGetValue(sensorDevice.Id, out var coolingDevice))
			{
				coolingDevice.UnbindSensors(sensorDevice);
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

	public Task<ICoolingService> GetCoolerServiceAsync(CancellationToken cancellationToken)
		=> _connectionManager.GetCoolingServiceAsync(cancellationToken);
}

internal sealed class CoolingDeviceViewModel : BindableObject, IDisposable
{
	private readonly DeviceViewModel _deviceViewModel;
	private SensorDeviceViewModel? _sensorDeviceViewModel;
	private CoolingDeviceInformation _coolingDeviceInformation;
	private readonly ObservableCollection<CoolerViewModel> _coolers;
	private readonly Dictionary<Guid, CoolerViewModel> _coolersById;
	private readonly Dictionary<Guid, CoolerViewModel> _coolersBySensorId;
	private bool _isExpanded;

	public Guid Id => _coolingDeviceInformation.DeviceId;
	public DeviceCategory Category => _deviceViewModel.Category;
	public string FriendlyName => _deviceViewModel.FriendlyName;
	public bool IsAvailable => _deviceViewModel.IsAvailable;

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	public ObservableCollection<CoolerViewModel> Coolers => _coolers;

	public CoolingDeviceViewModel(CoolingViewModel coolingViewModel, DeviceViewModel deviceViewModel, SensorDeviceViewModel? sensorDeviceViewModel, CoolingDeviceInformation coolingDeviceInformation)
	{
		_deviceViewModel = deviceViewModel;
		_sensorDeviceViewModel = sensorDeviceViewModel;
		_coolingDeviceInformation = coolingDeviceInformation;
		_coolers = new();
		_coolersById = new();
		_coolersBySensorId = new();
		_deviceViewModel.PropertyChanged += OnDeviceViewModelPropertyChanged;
		if (sensorDeviceViewModel is not null)
		{
			sensorDeviceViewModel.Sensors.CollectionChanged += OnSensorCollectionChanged;
		}
		UpdateDeviceInformation(coolingDeviceInformation);
	}

	public void Dispose()
	{
		_deviceViewModel.PropertyChanged -= OnDeviceViewModelPropertyChanged;
		OnDeviceOffline();
	}

	private void OnDeviceViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.IsAvailable))
		{
			// Device going online is already handled by UpdateDeviceInformation, but we need to handle the device going offline too.
			if (((DeviceViewModel)sender!).IsAvailable)
			{
				OnDeviceOnline();
			}
			else
			{
				OnDeviceOffline();
			}
		}
		else if (!(Equals(e, ChangedProperty.Category) || Equals(e, ChangedProperty.FriendlyName)))
		{
			return;
		}

		NotifyPropertyChanged(e);
	}

	public void UpdateDeviceInformation(CoolingDeviceInformation information)
	{
		// Currently, the only info contained here is the list of cooling.
		_coolingDeviceInformation = information;

		// NB: Ideally, the list of cooling should never change, but we at least want to support driver updates where new cooling are handled.

		// Reference all currently known cooler IDs.
		var coolerIds = new HashSet<Guid>(_coolersById.Keys);

		// Detect removed cooling by eliminating non-removed cooling from the set.
		foreach (var coolerInfo in information.Coolers)
		{
			coolerIds.Remove(coolerInfo.CoolerId);
		}

		// Actually remove the cooling that need to be removed.
		foreach (var coolerId in coolerIds)
		{
			if (_coolersById.Remove(coolerId, out var vm))
			{
				_coolers.Remove(vm);
				if (vm.SpeedSensorId is Guid sensorId)
				{
					_coolersBySensorId.Remove(sensorId);
				}
			}
		}

		// Add or update the cooling.
		// TODO: Manage the cooler order somehow? (Should be doable by adding the index in the viewmodel and inserting at the proper place)
		bool isOnline = _deviceViewModel.IsAvailable;
		foreach (var coolerInfo in information.Coolers)
		{
			var speedSensor = coolerInfo.SpeedSensorId is Guid sensorId ? _sensorDeviceViewModel?.GetSensor(sensorId) : null;

			if (!_coolersById.TryGetValue(coolerInfo.CoolerId, out var vm))
			{
				vm = new CoolerViewModel(this, coolerInfo, speedSensor);
				if (isOnline)
				{
					vm.SetOnline(coolerInfo);
				}
				_coolersById.Add(coolerInfo.CoolerId, vm);
				_coolers.Add(vm);
			}
			else
			{
				if (isOnline)
				{
					vm.SetOnline(coolerInfo);
				}
				else
				{
					vm.SetOffline();
				}
			}
		}
	}

	private void OnDeviceOnline()
	{
		foreach (var cooler in _coolers)
		{
			cooler.SetOnline();
		}
	}

	private void OnDeviceOffline()
	{
		foreach (var cooler in _coolers)
		{
			cooler.SetOffline();
		}
	}

	public void BindSensors(SensorDeviceViewModel sensorDeviceViewModel)
	{
		if (_sensorDeviceViewModel == sensorDeviceViewModel || sensorDeviceViewModel is null) return;
		_sensorDeviceViewModel = sensorDeviceViewModel;
		foreach (var cooler in _coolers)
		{
			cooler.OnSensorsBound(sensorDeviceViewModel);
		}
		sensorDeviceViewModel.Sensors.CollectionChanged += OnSensorCollectionChanged;
	}

	public void UnbindSensors(SensorDeviceViewModel sensorDeviceViewModel)
	{
		if (_sensorDeviceViewModel != sensorDeviceViewModel) return;
		sensorDeviceViewModel.Sensors.CollectionChanged -= OnSensorCollectionChanged;
		_sensorDeviceViewModel = null;
		foreach (var cooler in _coolers)
		{
			cooler.OnSensorsUnbound();
		}
	}

	private void OnSensorCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			var sensorDevice = (SensorDeviceViewModel)e.NewItems![0]!;
		}
		else if (e.Action == NotifyCollectionChangedAction.Remove)
		{
			var sensorDevice = (SensorDeviceViewModel)e.OldItems![0]!;
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
}

internal sealed class CoolerViewModel : BindableObject
{
	public CoolingDeviceViewModel Device { get; }
	private CoolerInformation _coolerInformation;
	private SensorViewModel? _speedSensor;
	private bool _isExpanded;

	public Guid Id => _coolerInformation.CoolerId;
	public Guid? SpeedSensorId => _coolerInformation.SpeedSensorId;
	public SensorViewModel? SpeedSensor => _speedSensor;

	public CoolerViewModel(CoolingDeviceViewModel device, CoolerInformation coolerInformation, SensorViewModel? speedSensor)
	{
		Device = device;
		_coolerInformation = coolerInformation;
		_speedSensor = speedSensor;
	}

	public string DisplayName => CoolerDatabase.GetCoolerDisplayName(_coolerInformation.CoolerId) ?? string.Create(CultureInfo.InvariantCulture, $"Cooler {_coolerInformation.CoolerId:B}.");
	public CoolerType Type => _coolerInformation.Type;

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	public void SetOnline()
	{
	}

	public void SetOnline(CoolerInformation information)
	{
		var oldInfo = _coolerInformation;
		_coolerInformation = information;
	}

	public void SetOffline()
	{
	}

	public void OnSensorsBound(SensorDeviceViewModel sensorDeviceViewModel)
	{
		if (_coolerInformation.SpeedSensorId is Guid sensorId)
		{
			if (sensorDeviceViewModel.GetSensor(sensorId) is { } speedSensor)
			{
				SetValue(ref _speedSensor, speedSensor, ChangedProperty.SpeedSensor);
			}
		}
	}

	public void OnSensorsUnbound()
	{
		SetValue(ref _speedSensor, null, ChangedProperty.SpeedSensor);
	}
}
