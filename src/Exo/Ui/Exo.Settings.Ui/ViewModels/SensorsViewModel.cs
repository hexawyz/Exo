using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Exo.Contracts.Ui.Settings;
using Exo.Metadata;
using Exo.Settings.Ui.Controls;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SensorsViewModel : IAsyncDisposable, IConnectedState
{
	private readonly SettingsServiceConnectionManager _connectionManager;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly Services.ISensorService _sensorService;
	private readonly ISettingsMetadataService _metadataService;
	private readonly ObservableCollection<SensorDeviceViewModel> _sensorDevices;
	private readonly ObservableCollection<SensorViewModel> _sensorsAvailableForCoolingControlCurves;
	private readonly Dictionary<Guid, SensorDeviceViewModel> _sensorDevicesById;
	private readonly Dictionary<Guid, SensorDeviceInformation> _pendingDeviceInformations;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public ObservableCollection<SensorDeviceViewModel> Devices => _sensorDevices;
	public ObservableCollection<SensorViewModel> SensorsAvailableForCoolingControlCurves => _sensorsAvailableForCoolingControlCurves;

	public SensorsViewModel(SettingsServiceConnectionManager connectionManager, DevicesViewModel devicesViewModel, Services.ISensorService sensorService, ISettingsMetadataService metadataService)
	{
		_connectionManager = connectionManager;
		_devicesViewModel = devicesViewModel;
		_sensorService = sensorService;
		_metadataService = metadataService;
		_sensorDevices = new();
		_sensorsAvailableForCoolingControlCurves = new();
		_sensorDevicesById = new();
		_pendingDeviceInformations = new();
		_cancellationTokenSource = new();
		_devicesViewModel.Devices.CollectionChanged += OnDevicesCollectionChanged;
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
			await _metadataService.WaitForAvailabilityAsync(cancellationToken);
			await WatchDevicesAsync(cts.Token);
		}
	}

	void IConnectedState.Reset()
	{
		_sensorDevicesById.Clear();
		_pendingDeviceInformations.Clear();

		foreach (var device in _sensorDevices)
		{
			device.Dispose();
		}

		_sensorDevices.Clear();
		_sensorsAvailableForCoolingControlCurves.Clear();
	}

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var sensorService = await _connectionManager.GetSensorServiceAsync(cancellationToken);
			await foreach (var info in sensorService.WatchSensorDevicesAsync(cancellationToken))
			{
				if (_sensorDevicesById.TryGetValue(info.DeviceId, out var vm))
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
						_pendingDeviceInformations[info.DeviceId] = info;
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

	private void OnDeviceAdded(DeviceViewModel device, SensorDeviceInformation sensorDeviceInformation)
	{
		var vm = new SensorDeviceViewModel(this, device, sensorDeviceInformation, _metadataService, _sensorsAvailableForCoolingControlCurves);
		_sensorDevices.Add(vm);
		_sensorDevicesById[vm.Id] = vm;
	}

	private void OnDeviceChanged(SensorDeviceViewModel viewModel, SensorDeviceInformation sensorDeviceInformation)
	{
		viewModel.UpdateDeviceInformation(sensorDeviceInformation, _sensorsAvailableForCoolingControlCurves);
	}

	private void OnDeviceRemoved(Guid deviceId)
	{
		for (int i = 0; i < _sensorDevices.Count; i++)
		{
			var vm = _sensorDevices[i];
			if (_sensorDevices[i].Id == deviceId)
			{
				_sensorDevices.RemoveAt(i);
				_sensorDevicesById.Remove(vm.Id);
				break;
			}
		}
	}

	public Services.ISensorService SensorService => _sensorService;

	public SensorDeviceViewModel? GetDevice(Guid deviceId)
	{
		_sensorDevicesById.TryGetValue(deviceId, out var device);
		return device;
	}
}

internal sealed class SensorDeviceViewModel : BindableObject, IDisposable
{
	private readonly DeviceViewModel _deviceViewModel;
	private SensorDeviceInformation _sensorDeviceInformation;
	private readonly ISettingsMetadataService _metadataService;

	public SensorsViewModel SensorsViewModel { get; }
	private readonly ObservableCollection<SensorViewModel> _sensors;
	private readonly Dictionary<Guid, SensorViewModel> _sensorsById;
	private bool _isExpanded;

	public Guid Id => _sensorDeviceInformation.DeviceId;
	public DeviceCategory Category => _deviceViewModel.Category;
	public string FriendlyName => _deviceViewModel.FriendlyName;
	public bool IsAvailable => _deviceViewModel.IsAvailable;

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetValue(ref _isExpanded, value, ChangedProperty.IsExpanded);
	}

	public ObservableCollection<SensorViewModel> Sensors => _sensors;

	public SensorDeviceViewModel
	(
		SensorsViewModel sensorsViewModel,
		DeviceViewModel deviceViewModel,
		SensorDeviceInformation sensorDeviceInformation,
		ISettingsMetadataService metadataService,
		ObservableCollection<SensorViewModel> sensorsAvailableForCoolingControlCurves
	)
	{
		_deviceViewModel = deviceViewModel;
		_sensorDeviceInformation = sensorDeviceInformation;
		_metadataService = metadataService;
		SensorsViewModel = sensorsViewModel;
		_sensors = new();
		_sensorsById = new();
		_deviceViewModel.PropertyChanged += OnDeviceViewModelPropertyChanged;
		UpdateDeviceInformation(sensorDeviceInformation, sensorsAvailableForCoolingControlCurves);
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

	public void UpdateDeviceInformation(SensorDeviceInformation information, ObservableCollection<SensorViewModel> sensorsAvailableForCoolingControlCurves)
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
				if (vm.PresetControlCurveSteps.Length > 0)
				{
					sensorsAvailableForCoolingControlCurves.Remove(vm);
				}
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
				vm = new SensorViewModel(this, sensorInfo, _metadataService);
				if (isOnline)
				{
					vm.SetOnline(sensorInfo);
				}
				_sensorsById.Add(sensorInfo.SensorId, vm);
				_sensors.Add(vm);
				if (vm.PresetControlCurveSteps.Length > 0)
				{
					sensorsAvailableForCoolingControlCurves.Add(vm);
				}
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

	private void OnDeviceOnline()
	{
		foreach (var sensor in _sensors)
		{
			sensor.SetOnline();
		}
	}

	private void OnDeviceOffline()
	{
		foreach (var sensor in _sensors)
		{
			sensor.SetOffline();
		}
	}

	public SensorViewModel? GetSensor(Guid sensorId)
	{
		_sensorsById.TryGetValue(sensorId, out var sensor);
		return sensor;
	}
}

internal sealed class SensorViewModel : BindableObject
{
	public SensorDeviceViewModel Device { get; }
	private SensorInformation _sensorInformation;
	private LiveSensorDetailsViewModel? _liveDetails;
	private readonly string _displayName;
	private readonly SensorCategory _sensorCategory;
	private readonly double? _metadataMinimumValue;
	private readonly double? _metadataMaximumValue;
	private readonly double[] _presetControlCurveSteps;

	public Guid Id => _sensorInformation.SensorId;

	public SensorViewModel(SensorDeviceViewModel device, SensorInformation sensorInformation, ISettingsMetadataService metadataService)
	{
		Device = device;
		_sensorInformation = sensorInformation;
		string? displayName = null;
		if (metadataService.TryGetSensorMetadata("", "", sensorInformation.SensorId, out var metadata))
		{
			displayName = metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
			_sensorCategory = metadata.Category;
			_metadataMinimumValue = metadata.MinimumValue;
			_metadataMaximumValue = metadata.MaximumValue;
			_presetControlCurveSteps = metadata.PresetControlCurveSteps ?? [];
		}
		else
		{
			_sensorCategory = _sensorInformation.Unit switch
			{
				"%" => SensorCategory.Load,
				"Hz" or "kHz" or "MHz" or "GHz" => SensorCategory.Frequency,
				"W" => SensorCategory.Power,
				"V" => SensorCategory.Voltage,
				"A" => SensorCategory.Current,
				"°C" or "°F" or "°K" => SensorCategory.Temperature,
				"RPM" => SensorCategory.Fan,
				_ => SensorCategory.Other,
			};
			_presetControlCurveSteps = [];
		}
		_displayName = displayName ?? string.Create(CultureInfo.InvariantCulture, $"Sensor {_sensorInformation.SensorId:B}.");
	}

	public string DisplayName => _displayName;
	public SensorDataType DataType => _sensorInformation.DataType;
	public string Unit => _sensorInformation.Unit;
	public SensorCapabilities Capabilities => _sensorInformation.Capabilities;
	public double? ScaleMinimumValue => _sensorInformation.ScaleMinimumValue ?? _metadataMinimumValue;
	public double? ScaleMaximumValue => _sensorInformation.ScaleMaximumValue ?? _metadataMaximumValue;
	public LiveSensorDetailsViewModel? LiveDetails => _liveDetails;
	public SensorCategory Category => _sensorCategory;
	public ImmutableArray<double> PresetControlCurveSteps => ImmutableCollectionsMarshal.AsImmutableArray(_presetControlCurveSteps);
	public string FullDisplayName => $"{Device.FriendlyName} - {_displayName}";

	public void SetOnline() => StartWatching();

	public void SetOnline(SensorInformation information)
	{
		var oldInfo = _sensorInformation;
		_sensorInformation = information;

		if (oldInfo.DataType != _sensorInformation.DataType) NotifyPropertyChanged(ChangedProperty.DataType);
		if (oldInfo.Unit != _sensorInformation.Unit) NotifyPropertyChanged(ChangedProperty.Unit);
		if (oldInfo.Capabilities != _sensorInformation.Capabilities) NotifyPropertyChanged(ChangedProperty.Capabilities);

		if ((_sensorInformation.Capabilities & (SensorCapabilities.Polled | SensorCapabilities.Streamed)) != 0)
		{
			StartWatching();
		}
	}

	private void StartWatching()
	{
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
		await liveDetails.DisposeAsync();
		NotifyPropertyChanged(ChangedProperty.LiveDetails);
	}
}

internal sealed class LiveSensorDetailsViewModel : BindableObject, IAsyncDisposable
{
	// This is a public wrapper that is used to expose the data and allow it to be rendered into a chart.
	public sealed class HistoryData : ITimeSeries
	{
		private readonly LiveSensorDetailsViewModel _viewModel;

		public HistoryData(LiveSensorDetailsViewModel viewModel) => _viewModel = viewModel;

		public DateTime StartTime => _viewModel._currentValueTime;

		public int Length => _viewModel._dataPoints.Length;

		public double this[int index]
		{
			get
			{
				var vm = _viewModel;
				if ((uint)index >= (uint)vm._dataPoints.Length) throw new ArgumentOutOfRangeException(nameof(index));
				index += vm._currentPointIndex + 1;
				int roundTrippedIndex = index - vm._dataPoints.Length;
				return vm._dataPoints[roundTrippedIndex < 0 ? index : roundTrippedIndex];
			}
		}

		public TimeSpan Interval => new(TimeSpan.TicksPerSecond);

		public event EventHandler? Changed;

		public void NotifyChange() => Changed?.Invoke(this, EventArgs.Empty);

		public double? MaximumReachedValue => _viewModel._maxValue;
		public double? MinimumReachedValue => _viewModel._minValue;
	}

	private const int WindowSizeInSeconds = 1 * 60;

	private double _currentValue;
	private double _minValue;
	private double _maxValue;
	private DateTime _currentValueTime;
	private ulong _currentTimestampInSeconds;
	private int _currentPointIndex;
	private readonly double[] _dataPoints;
	private readonly SensorViewModel _sensor;
	private readonly HistoryData _historyData;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public LiveSensorDetailsViewModel(SensorViewModel sensor)
	{
		_currentValue = double.NaN;
		_minValue = double.PositiveInfinity;
		_maxValue = double.NegativeInfinity;
		_currentValueTime = DateTime.UtcNow;
		_currentTimestampInSeconds = GetTimestamp();
		_dataPoints = new double[WindowSizeInSeconds];
		_sensor = sensor;
		_historyData = new(this);
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchTask;
	}

	public NumberWithUnit CurrentValue => new(_currentValue, _sensor.Unit);
	public HistoryData History => _historyData;

	private static ulong GetTimestamp() => (ulong)Stopwatch.GetTimestamp() / (ulong)Stopwatch.Frequency;

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			switch (_sensor.DataType)
			{
			case SensorDataType.UInt8: await WatchAsync<byte>(cancellationToken).ConfigureAwait(false);break;
			case SensorDataType.UInt16: await WatchAsync<ushort>(cancellationToken).ConfigureAwait(false);break;
			case SensorDataType.UInt32: await WatchAsync<uint>(cancellationToken).ConfigureAwait(false);break;
			case SensorDataType.UInt64: await WatchAsync<ulong>(cancellationToken).ConfigureAwait(false);break;
			case SensorDataType.UInt128: await WatchAsync<UInt128>(cancellationToken).ConfigureAwait(false);break;
			case SensorDataType.SInt8: await WatchAsync<sbyte>(cancellationToken).ConfigureAwait(false); break;
			case SensorDataType.SInt16: await WatchAsync<short>(cancellationToken).ConfigureAwait(false); break;
			case SensorDataType.SInt32: await WatchAsync<int>(cancellationToken).ConfigureAwait(false); break;
			case SensorDataType.SInt64: await WatchAsync<long>(cancellationToken).ConfigureAwait(false); break;
			case SensorDataType.SInt128: await WatchAsync<Int128>(cancellationToken).ConfigureAwait(false); break;
			case SensorDataType.Float16: await WatchAsync<Half>(cancellationToken).ConfigureAwait(false); break;
			case SensorDataType.Float32: await WatchAsync<float>(cancellationToken).ConfigureAwait(false); break;
			case SensorDataType.Float64: await WatchAsync<double>(cancellationToken).ConfigureAwait(false); break;
			default: throw new InvalidOperationException("Unsupported data type.");
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			// NB: Should generally be an RpcException or ObjectDisposedException.
		}
	}

	private async Task WatchAsync<TValue>(CancellationToken cancellationToken)
		where TValue : unmanaged, INumber<TValue>
	{
		var sensorService = _sensor.Device.SensorsViewModel.SensorService;
		await foreach (var dataPoint in sensorService.WatchValuesAsync<TValue>(_sensor.Device.Id, _sensor.Id, cancellationToken))
		{
			// Get the timestamp for the current data point.
			var now = DateTime.UtcNow;
			var currentTimestamp = GetTimestamp();
			ulong delta = currentTimestamp - _currentTimestampInSeconds;
			// Backfill any missing data points using the previous value.
			if (delta > 1)
			{
				// If we are late for longer than the window size, we can just clear up the whole window and restart at index 0.
				// NB: In the very unlikely occasion where the timer would wrap-around, it would be handled by this condition. We'd end up resetting the history, which is not that terrible.
				if (delta > (ulong)_dataPoints.Length)
				{
					Array.Fill(_dataPoints, _currentValue, 0, _dataPoints.Length);
					_currentPointIndex = 0;
				}
				else
				{
					// If we are late for less than the window size, the operation might need to be split in two.
					int endIndex = _currentPointIndex + (int)delta;
					if (++_currentPointIndex < _dataPoints.Length)
					{
						Array.Fill(_dataPoints, _currentValue, _currentPointIndex, Math.Min(_dataPoints.Length, endIndex) - _currentPointIndex);
					}
					if (endIndex >= _dataPoints.Length)
					{
						_currentPointIndex = endIndex - _dataPoints.Length;
						if (_currentPointIndex > 0)
						{
							Array.Fill(_dataPoints, _currentValue, 0, _currentPointIndex - 1);
						}
					}
					else
					{
						_currentPointIndex = endIndex;
					}
				}
			}
			else if (delta == 1)
			{
				// Increase the index
				if (++_currentPointIndex == _dataPoints.Length) _currentPointIndex = 0;
			}
			_currentValueTime = now;
			_currentTimestampInSeconds = currentTimestamp;
			var doubleValue = double.CreateSaturating(dataPoint);
			_dataPoints[_currentPointIndex] = doubleValue;
			if (doubleValue < _minValue) _minValue = doubleValue;
			if (doubleValue > _maxValue) _maxValue = doubleValue;
			SetValue(ref _currentValue, doubleValue, ChangedProperty.CurrentValue);
			History.NotifyChange();
		}
	}
}

internal readonly struct NumberWithUnit
{
	public NumberWithUnit(double value, string? symbol)
	{
		Value = value;
		Symbol = symbol;
	}

	public double Value { get; }
	public string? Symbol { get; }

	public override string ToString()
	{
		if (Symbol is not { } symbol) return Value.ToString("G3");

		var value = Value;
		if (symbol == "RPM")
		{
			return $"{value:N0}\xA0{symbol}";
		}
		if (value > 1000)
		{
			if (symbol == "Hz")
			{
				if (Value > 1_000_000_000)
				{
					value *= 0.000000001;
					symbol = "GHz";
				}
				else if (Value > 1_000_000)
				{
					value *= 0.000001;
					symbol = "MHz";
				}
				else
				{
					value *= 0.001;
					symbol = "kHz";
				}
			}
			else if (symbol == "kHz")
			{
				if (Value > 1000_000)
				{
					value *= 0.000001;
					symbol = "GHz";
				}
				else
				{
					value *= 0.001;
					symbol = "MHz";
				}
			}
			else if (symbol == "MHz")
			{
				value *= 0.001;
				symbol = "GHz";
			}
		}
		return $"{value:G3}\xA0{symbol}";
	}
}
