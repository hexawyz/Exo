using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using CommunityToolkit.WinUI;
using Exo.Contracts.Ui.Settings;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SensorsViewModel
{
	private readonly ISensorService _sensorService;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly ObservableCollection<SensorDeviceViewModel> _sensorDevices;
	private readonly Dictionary<Guid, SensorDeviceViewModel> _sensorDeviceById;
	private readonly Dictionary<Guid, SensorDeviceInformation> _pendingDeviceInformations;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchDevicesTask;

	public ObservableCollection<SensorDeviceViewModel> Devices => _sensorDevices;

	public SensorsViewModel(ISensorService sensorService, DevicesViewModel devicesViewModel)
	{
		_sensorService = sensorService;
		_devicesViewModel = devicesViewModel;
		_sensorDevices = new();
		_sensorDeviceById = new();
		_pendingDeviceInformations = new();
		_cancellationTokenSource = new();
		_watchDevicesTask = WatchDevicesAsync(_cancellationTokenSource.Token);
		_devicesViewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchDevicesTask.ConfigureAwait(false);
	}

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var info in _sensorService.WatchSensorDevicesAsync(cancellationToken))
			{
				if (_sensorDeviceById.TryGetValue(info.DeviceId, out var vm))
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
		else
		{
			// As of writing this code, we don't require support for anything else, but if this change in the future, this exception will be triggered.
			throw new InvalidOperationException("This case is not handled.");
		}
	}

	private void OnDeviceAdded(DeviceViewModel device, SensorDeviceInformation sensorDeviceInformation)
	{
		var vm = new SensorDeviceViewModel(this, device, sensorDeviceInformation);
		_sensorDevices.Add(vm);
		_sensorDeviceById[vm.Id] = vm;
	}

	private void OnDeviceRemoved(Guid deviceId)
	{
		for (int i = 0; i < _sensorDevices.Count; i++)
		{
			var vm = _sensorDevices[i];
			if (_sensorDevices[i].Id == deviceId)
			{
				_sensorDevices.RemoveAt(i);
				_sensorDeviceById.Remove(vm.Id);
				break;
			}
		}
	}

	public IAsyncEnumerable<SensorDataPoint> WatchValuesAsync(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
		=> _sensorService.WatchValuesAsync(new() { DeviceId = deviceId, SensorId = sensorId }, cancellationToken);
}

internal sealed class SensorDeviceViewModel
{
	private readonly DeviceViewModel _deviceViewModel;
	private SensorDeviceInformation _sensorDeviceInformation;
	public SensorsViewModel SensorsViewModel { get; }
	private readonly ObservableCollection<SensorViewModel> _sensors;
	private readonly Dictionary<Guid, SensorViewModel> _sensorsById;

	public Guid Id => _sensorDeviceInformation.DeviceId;
	public DeviceCategory Category => _deviceViewModel.Category;
	public string FriendlyName => _deviceViewModel.FriendlyName;
	public bool IsAvailable => _deviceViewModel.IsAvailable;

	public ObservableCollection<SensorViewModel> Sensors => _sensors;

	public SensorDeviceViewModel(SensorsViewModel sensorsViewModel, DeviceViewModel deviceViewModel, SensorDeviceInformation sensorDeviceInformation)
	{
		_deviceViewModel = deviceViewModel;
		_sensorDeviceInformation = sensorDeviceInformation;
		SensorsViewModel = sensorsViewModel;
		_sensors = new();
		_sensorsById = new();
		_deviceViewModel.PropertyChanged += OnDeviceViewModelPropertyChanged;
		UpdateDeviceInformation(sensorDeviceInformation);
	}

	private void OnDeviceViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e == ChangedProperty.IsAvailable)
		{
			// Device going online is already handled by UpdateDeviceInformation, but we need to handle the device going offline too.
			if (!((DeviceViewModel)sender!).IsAvailable)
			{
				OnDeviceOffline();
			}
		}
	}

	public void UpdateDeviceInformation(SensorDeviceInformation information)
	{
		// Currently, the only info contained here is the list of sensors.
		_sensorDeviceInformation = information;

		// NB: Ideally, the list of sensors should never change, but we at least want to support driver updates where new sensors are handled.

		// Reference all currently known sensor IDs.
		var sensorIds = new HashSet<Guid>(_sensorsById.Keys);

		// Detect removed sensors by eliminating non-removed sensors from the set.
		foreach (var sensorInfo in information.Sensors)
		{
			sensorIds.Remove(sensorInfo.SensorId);
		}

		// Actually remove the sensors that need to be removed.
		foreach (var sensorId in sensorIds)
		{
			if (_sensorsById.Remove(sensorId, out var vm))
			{
				_sensors.Remove(vm);
			}
		}

		// Add or update the sensors.
		// TODO: Manage the sensor order somehow? (Should be doable by adding the index in the viewmodel and inserting at the proper place)
		bool isOnline = _deviceViewModel.IsAvailable;
		foreach (var sensorInfo in information.Sensors)
		{
			if (!_sensorsById.TryGetValue(sensorInfo.SensorId, out var vm))
			{
				vm = new SensorViewModel(this, sensorInfo);
				if (isOnline)
				{
					vm.SetOnline(sensorInfo);
				}
				_sensorsById.Add(sensorInfo.SensorId, vm);
				_sensors.Add(vm);
			}
			else
			{
				if (isOnline)
				{
					vm.SetOnline(sensorInfo);
				}
				else
				{
					vm.SetOffline();
				}
			}
		}
	}

	private void OnDeviceOffline()
	{
		foreach (var sensor in _sensors)
		{
			sensor.SetOffline();
		}
	}
}

internal sealed class SensorViewModel : BindableObject
{
	public SensorDeviceViewModel Device { get; }
	private SensorInformation _sensorInformation;
	private LiveSensorDetailsViewModel? _liveDetails;

	public Guid Id => _sensorInformation.SensorId;

	public SensorViewModel(SensorDeviceViewModel device, SensorInformation sensorInformation)
	{
		Device = device;
		_sensorInformation = sensorInformation;
	}

	public string DisplayName => SensorDatabase.GetSensorDisplayName(_sensorInformation.SensorId) ?? string.Create(CultureInfo.InvariantCulture, $"Effect {_sensorInformation.SensorId:B}.");
	public SensorDataType DataType => _sensorInformation.DataType;
	public bool IsPolled => _sensorInformation.IsPolled;
	public LiveSensorDetailsViewModel? LiveDetails => _liveDetails;

	public void SetOnline(SensorInformation information)
	{
		var oldInfo = _sensorInformation;
		_sensorInformation = information;

		if (oldInfo.DataType != _sensorInformation.DataType) NotifyPropertyChanged(ChangedProperty.DataType);
		if (oldInfo.IsPolled != _sensorInformation.IsPolled) NotifyPropertyChanged(ChangedProperty.IsPolled);

		if (_liveDetails is null)
		{
			_liveDetails = new(this);
			NotifyPropertyChanged(ChangedProperty.LiveDetails);
		}
	}

	public async void SetOffline()
	{
		if (_liveDetails is not { } liveDetails) return;
		_liveDetails = null;
		NotifyPropertyChanged(ChangedProperty.LiveDetails);
		await liveDetails.DisposeAsync();
	}
}

internal sealed class LiveSensorDetailsViewModel : BindableObject, IAsyncDisposable
{
	private double _currentValue;
	private readonly SensorViewModel _sensor;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public LiveSensorDetailsViewModel(SensorViewModel sensor)
	{
		_sensor = sensor;
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchTask;
	}

	public double CurrentValue
	{
		get => _currentValue;
		set => SetValue(ref _currentValue, value, ChangedProperty.CurrentValue);
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		await foreach (var dataPoint in _sensor.Device.SensorsViewModel.WatchValuesAsync(_sensor.Device.Id, _sensor.Id, cancellationToken))
		{
			CurrentValue = dataPoint.Value;
		}
	}
}