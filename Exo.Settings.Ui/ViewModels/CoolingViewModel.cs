using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Controls;
using Exo.Settings.Ui.Services;
using Exo.Ui;
using RawCoolingModes = Exo.Contracts.Ui.Settings.CoolingModes;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class CoolingViewModel : IAsyncDisposable, IConnectedState
{
	private readonly SettingsServiceConnectionManager _connectionManager;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly SensorsViewModel _sensorsViewModel;
	private readonly ISettingsMetadataService _metadataService;
	private readonly ObservableCollection<CoolingDeviceViewModel> _coolingDevices;
	private readonly Dictionary<Guid, CoolingDeviceViewModel> _coolingDevicesById;
	private readonly Dictionary<Guid, CoolingDeviceInformation> _pendingDeviceInformations;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public ObservableCollection<CoolingDeviceViewModel> Devices => _coolingDevices;

	public CoolingViewModel(SettingsServiceConnectionManager connectionManager, DevicesViewModel devicesViewModel, SensorsViewModel sensorsViewModel, ISettingsMetadataService metadataService)
	{
		_connectionManager = connectionManager;
		_devicesViewModel = devicesViewModel;
		_sensorsViewModel = sensorsViewModel;
		_metadataService = metadataService;
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
			await _metadataService.WaitForAvailabilityAsync(cancellationToken);
			await WatchDevicesAsync(cts.Token);
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
		var vm = new CoolingDeviceViewModel(device, _sensorsViewModel.GetDevice(device.Id), coolingDeviceInformation, _sensorsViewModel, _metadataService);
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
	private readonly ISettingsMetadataService _metadataService;
	private readonly ObservableCollection<CoolerViewModel> _coolers;
	private readonly Dictionary<Guid, CoolerViewModel> _coolersById;
	private readonly Dictionary<Guid, CoolerViewModel> _coolersBySensorId;
	private bool _isExpanded;

	private readonly SensorsViewModel _sensorsViewModel;

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

	public CoolingDeviceViewModel(DeviceViewModel deviceViewModel, SensorDeviceViewModel? sensorDeviceViewModel, CoolingDeviceInformation coolingDeviceInformation, SensorsViewModel sensorsViewModel, ISettingsMetadataService metadataService)
	{
		_deviceViewModel = deviceViewModel;
		_sensorDeviceViewModel = sensorDeviceViewModel;
		_coolingDeviceInformation = coolingDeviceInformation;
		_metadataService = metadataService;
		_coolers = new();
		_coolersById = new();
		_coolersBySensorId = new();
		_sensorsViewModel = sensorsViewModel;
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
				vm = new CoolerViewModel(this, coolerInfo, _sensorsViewModel, speedSensor, _metadataService);
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

internal sealed class CoolerViewModel : ChangeableBindableObject
{
	private static class Commands
	{
		public sealed class ResetCommand : ICommand
		{
			public event EventHandler? CanExecuteChanged;

			public ResetCommand(CoolerViewModel cooler) { }

			public bool CanExecute(object? parameter) => parameter is CoolerViewModel;
			public void Execute(object? parameter) { }

			internal void OnChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public CoolingDeviceViewModel Device { get; }
	private ReadOnlyCollection<ICoolingModeViewModel> _coolingModes;

	private CoolerInformation _coolerInformation;
	private SensorViewModel? _speedSensor;
	private readonly string _coolerDisplayName;
	private bool _isExpanded;

	private ICoolingModeViewModel? _initialCoolingMode;
	private ICoolingModeViewModel? _currentCoolingMode;

	private readonly SensorsViewModel _sensorsViewModel;

	private readonly Commands.ResetCommand _resetCommand;

	public override bool IsChanged => _initialCoolingMode != _currentCoolingMode;

	public Guid Id => _coolerInformation.CoolerId;
	public Guid? SpeedSensorId => _coolerInformation.SpeedSensorId;
	public SensorViewModel? SpeedSensor => _speedSensor;

	public ReadOnlyCollection<ICoolingModeViewModel> CoolingModes => _coolingModes;

	public ICoolingModeViewModel? CurrentCoolingMode
	{
		get => _currentCoolingMode;
		set
		{
			bool wasChanged = IsChanged;
			if (SetValue(ref _currentCoolingMode, value, ChangedProperty.CurrentCoolingMode))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public ICommand ResetCommand => _resetCommand;

	public CoolerViewModel(CoolingDeviceViewModel device, CoolerInformation coolerInformation, SensorsViewModel sensorsViewModel, SensorViewModel? speedSensor, ISettingsMetadataService metadataService)
	{
		Device = device;
		_coolerInformation = coolerInformation;
		_sensorsViewModel = sensorsViewModel;
		if ((coolerInformation.SupportedCoolingModes & RawCoolingModes.Manual) != 0)
		{
			if (coolerInformation.PowerLimits is null) throw new InvalidOperationException("Power limits must not be null.");
		}

		var coolingModes = new List<ICoolingModeViewModel>();
		if ((coolerInformation.SupportedCoolingModes & RawCoolingModes.Automatic) != 0)
		{
			coolingModes.Add(AutomaticCoolingModeViewModel.Instance);
		}
		else if ((coolerInformation.SupportedCoolingModes & RawCoolingModes.Manual) != 0)
		{
			coolingModes.Add(new FixedCoolingModeViewModel(coolerInformation.PowerLimits!.MinimumPower, coolerInformation.PowerLimits.CanSwitchOff));
			coolingModes.Add(new ControlCurveCoolingModeViewModel(sensorsViewModel, coolerInformation.PowerLimits!.MinimumPower, coolerInformation.PowerLimits.CanSwitchOff));
		}

		if (coolingModes.Count > 0)
		{
			_coolingModes = Array.AsReadOnly(coolingModes.ToArray());
			_currentCoolingMode = _initialCoolingMode = coolingModes[0];
		}
		else
		{
			_coolingModes = ReadOnlyCollection<ICoolingModeViewModel>.Empty;
		}

		_speedSensor = speedSensor;
		string? displayName = null;
		if (metadataService.TryGetCoolerMetadata("", "", coolerInformation.CoolerId, out var metadata))
		{
			displayName = metadataService.GetString(CultureInfo.CurrentCulture, metadata.NameStringId);
		}
		_coolerDisplayName = displayName ?? string.Create(CultureInfo.InvariantCulture, $"Cooler {_coolerInformation.CoolerId:B}.");
		_resetCommand = new(this);
	}

	public string DisplayName => _coolerDisplayName;
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
		if (oldInfo.SupportedCoolingModes != information.SupportedCoolingModes)
		{
			// Take a snapshot of the pre-existing cooling modes in order to not allocate new instances.
			// This will be useful for updating the current and initial cooling modes later.
			ICoolingModeViewModel? automaticCoolingModeViewModel = null;
			ICoolingModeViewModel? fixedCoolingModeViewModel = null;
			ICoolingModeViewModel? controlCurveCoolingModeViewModel = null;

			foreach (var coolingMode in _coolingModes)
			{
				switch (coolingMode)
				{
				case AutomaticCoolingModeViewModel: automaticCoolingModeViewModel = coolingMode; break;
				case FixedCoolingModeViewModel: fixedCoolingModeViewModel = coolingMode; break;
				case ControlCurveCoolingModeViewModel: controlCurveCoolingModeViewModel = coolingMode; break;
				}
			}

			var coolingModes = new List<ICoolingModeViewModel>();
			if ((information.SupportedCoolingModes & RawCoolingModes.Automatic) != 0)
			{
				coolingModes.Add(automaticCoolingModeViewModel ?? AutomaticCoolingModeViewModel.Instance);
			}
			else if ((information.SupportedCoolingModes & RawCoolingModes.Manual) != 0)
			{
				coolingModes.Add(fixedCoolingModeViewModel ?? new FixedCoolingModeViewModel(information.PowerLimits!.MinimumPower, information.PowerLimits.CanSwitchOff));
				coolingModes.Add(controlCurveCoolingModeViewModel ?? new ControlCurveCoolingModeViewModel(_sensorsViewModel, information.PowerLimits!.MinimumPower, information.PowerLimits.CanSwitchOff));
			}
			_coolingModes = coolingModes.Count > 0 ? Array.AsReadOnly(coolingModes.ToArray()) : ReadOnlyCollection<ICoolingModeViewModel>.Empty;
			NotifyPropertyChanged(ChangedProperty.CoolingModes);
		}
		bool wasChanged = IsChanged;
		var oldInitialCoolingMode = _initialCoolingMode;
		var newInitialCoolingMode = _coolingModes.Count > 0 ? _coolingModes[0] : null;
		if (oldInitialCoolingMode != newInitialCoolingMode)
		{
			_initialCoolingMode = newInitialCoolingMode;
			if (_currentCoolingMode == oldInitialCoolingMode)
			{
				CurrentCoolingMode = _initialCoolingMode;
			}
		}
		if (_currentCoolingMode is not null ? !_coolingModes.Contains(_currentCoolingMode) : _initialCoolingMode is not null)
		{
			CurrentCoolingMode = _initialCoolingMode;
		}
		OnChangeStateChange(wasChanged);
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

	protected override void OnChanged()
	{
		_resetCommand.OnChanged();
		base.OnChanged();
	}
}

internal enum LogicalCoolingMode
{
	Automatic = 0,
	Fixed = 1,
	SoftwareControlCurve = 2,
	HardwareControlCurve = 3,
}

internal interface ICoolingModeViewModel : IResettable
{
	LogicalCoolingMode CoolingMode { get; }
}

internal sealed class AutomaticCoolingModeViewModel : ICoolingModeViewModel
{
	public static readonly AutomaticCoolingModeViewModel Instance = new();

	private AutomaticCoolingModeViewModel() { }

	public LogicalCoolingMode CoolingMode => LogicalCoolingMode.Automatic;
	public bool IsChanged => false;
	public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
	void IResettable.Reset() { }

}

internal sealed class FixedCoolingModeViewModel : ResettableBindableObject, ICoolingModeViewModel
{
	private static class Commands
	{
		public sealed class ResetPowerCommand : ICommand
		{
			public static readonly ResetPowerCommand Instance = new();

			private ResetPowerCommand() { }

			public void Execute(object? parameter) => ((FixedCoolingModeViewModel)parameter!).ResetPower();
			public bool CanExecute(object? parameter) => (parameter as FixedCoolingModeViewModel)?.IsChanged ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void NotifyCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	private byte _initialPower;
	private byte _currentPower;
	private readonly byte _minimumPower;
	private readonly bool _canSwitchOff;

	public FixedCoolingModeViewModel(byte minimumPower, bool canSwitchOff)
	{
		_currentPower = _initialPower = canSwitchOff ? (byte)0 : minimumPower;
		_minimumPower = minimumPower;
		_canSwitchOff = canSwitchOff;
	}

	public byte MinimumPower => _canSwitchOff ? (byte)0 : _minimumPower;
	public bool CanSwitchOff => _canSwitchOff;

	public override bool IsChanged => _currentPower != _initialPower;

	public LogicalCoolingMode CoolingMode => LogicalCoolingMode.Fixed;

	public ICommand ResetPowerCommand => Commands.ResetPowerCommand.Instance;

	public byte Power
	{
		get => _currentPower;
		set
		{
			if (value >= _minimumPower && value < 100 || _canSwitchOff && value == 0)
			{
				bool wasChanged = IsChanged;
				if (SetValue(ref _currentPower, value, ChangedProperty.Power))
				{
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	internal void SetInitialPower(byte value)
	{
		if (_initialPower != value)
		{
			byte oldValue = _initialPower;
			_initialPower = value;
			if (_currentPower == _initialPower)
			{
				_currentPower = value;
				NotifyPropertyChanged(ChangedProperty.Power);
			}
			else if (_currentPower == value)
			{
				OnChanged();
			}
		}
	}

	internal void ResetPower() => Power = _initialPower;

	protected override void Reset() => ResetPower();

	protected override void OnChanged()
	{
		Commands.ResetPowerCommand.NotifyCanExecuteChanged();
		base.OnChanged();
	}
}

internal sealed class ControlCurveCoolingModeViewModel : ResettableBindableObject, ICoolingModeViewModel
{
	private static class Commands
	{
		public sealed class ResetFallbackPowerCommand : ICommand
		{
			public static readonly ResetFallbackPowerCommand Instance = new();

			private ResetFallbackPowerCommand() { }

			public void Execute(object? parameter) => ((ControlCurveCoolingModeViewModel)parameter!).ResetFallbackPower();
			public bool CanExecute(object? parameter) => (parameter as ControlCurveCoolingModeViewModel)?.IsChanged ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void NotifyCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}

		public sealed class ResetInputSensorCommand : ICommand
		{
			public static readonly ResetInputSensorCommand Instance = new();

			private ResetInputSensorCommand() { }

			public void Execute(object? parameter) => ((ControlCurveCoolingModeViewModel)parameter!).ResetInputSensor();
			public bool CanExecute(object? parameter) => (parameter as ControlCurveCoolingModeViewModel)?.IsChanged ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void NotifyCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	private object? _points;

	private byte _initialFallbackPower;
	private byte _currentFallbackPower;
	private readonly byte _minimumPower;
	private readonly bool _canSwitchOff;

	public override bool IsChanged => _initialFallbackPower != _currentFallbackPower;

	public object? Points => _points;

	private readonly SensorsViewModel _sensorsViewModel;
	private SensorViewModel? _inputSensor;

	public LogicalCoolingMode CoolingMode => LogicalCoolingMode.SoftwareControlCurve;

	public ICommand ResetFallbackPowerCommand => Commands.ResetFallbackPowerCommand.Instance;
	public ICommand ResetInputSensorCommand => Commands.ResetInputSensorCommand.Instance;

	public byte MinimumOnPower => _minimumPower;
	public byte MinimumPower => _canSwitchOff ? (byte)0 : _minimumPower;
	public bool CanSwitchOff => _canSwitchOff;

	public byte FallbackPower
	{
		get => _currentFallbackPower;
		set
		{
			if (value >= _minimumPower && value < 100 || _canSwitchOff && value == 0)
			{
				bool wasChanged = IsChanged;
				if (SetValue(ref _currentFallbackPower, value, ChangedProperty.FallbackPower))
				{
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	public ObservableCollection<SensorViewModel> SensorsAvailableForCoolingControlCurves => _sensorsViewModel.SensorsAvailableForCoolingControlCurves;

	public SensorViewModel? InputSensor
	{
		get => _inputSensor;
		set
		{
			if (SetValue(ref _inputSensor, value, ChangedProperty.InputSensor))
			{
				_points = value is not null ? CreateNewDataPoints(value.DataType, value.PresetControlCurveSteps) : null;
				NotifyPropertyChanged(ChangedProperty.Points);
			}
		}
	}

	private static object CreateNewDataPoints(SensorDataType dataType, ImmutableArray<double> points)
		=> dataType switch
		{
			SensorDataType.UInt8 => CreateNewDataPoints<byte>(dataType, points),
			SensorDataType.UInt16 => CreateNewDataPoints<ushort>(dataType, points),
			SensorDataType.UInt32 => CreateNewDataPoints<uint>(dataType, points),
			SensorDataType.UInt64 => CreateNewDataPoints<ulong>(dataType, points),
			SensorDataType.UInt128 => CreateNewDataPoints<UInt128>(dataType, points),
			SensorDataType.SInt8 => CreateNewDataPoints<sbyte>(dataType, points),
			SensorDataType.SInt16 => CreateNewDataPoints<short>(dataType, points),
			SensorDataType.SInt32 => CreateNewDataPoints<int>(dataType, points),
			SensorDataType.SInt64 => CreateNewDataPoints<long>(dataType, points),
			SensorDataType.SInt128 => CreateNewDataPoints<Int128>(dataType, points),
			SensorDataType.Float16 => CreateNewDataPoints<Half>(dataType, points),
			SensorDataType.Float32 => CreateNewDataPoints<float>(dataType, points),
			SensorDataType.Float64 => CreateNewDataPoints<double>(dataType, points),
			_ => throw new InvalidOperationException(),
		};

	private static ObservableCollection<IDataPoint<T, byte>> CreateNewDataPoints<T>(SensorDataType dataType, ImmutableArray<double> points)
		where T : struct, INumber<T>
	{
		var collection = new ObservableCollection<IDataPoint<T, byte>>();
		foreach (double value in points)
		{
			collection.Add(new PowerDataPointViewModel<T>(T.CreateChecked(value), 100));
		}
		return collection;
	}

	public ControlCurveCoolingModeViewModel(SensorsViewModel sensorsViewModel, byte minimumPower, bool canSwitchOff)
	{
		_sensorsViewModel = sensorsViewModel;

		_currentFallbackPower = _initialFallbackPower = canSwitchOff ? (byte)0 : minimumPower;
		_minimumPower = minimumPower;
		_canSwitchOff = canSwitchOff;

		if (_sensorsViewModel.SensorsAvailableForCoolingControlCurves.Count > 0)
		{
			_inputSensor = _sensorsViewModel.SensorsAvailableForCoolingControlCurves[0];
			_points = CreateNewDataPoints(_inputSensor.DataType, _inputSensor.PresetControlCurveSteps);
		}
	}

	internal void SetInitialPower(byte value)
	{
		if (_initialFallbackPower != value)
		{
			byte oldValue = _initialFallbackPower;
			_initialFallbackPower = value;
			if (_currentFallbackPower == _initialFallbackPower)
			{
				_currentFallbackPower = value;
				NotifyPropertyChanged(ChangedProperty.FallbackPower);
			}
			else if (_currentFallbackPower == value)
			{
				OnChanged();
			}
		}
	}

	internal void ResetFallbackPower() => FallbackPower = _initialFallbackPower;
	internal void ResetInputSensor() => InputSensor = _sensorsViewModel.SensorsAvailableForCoolingControlCurves.Count > 0 ? _sensorsViewModel.SensorsAvailableForCoolingControlCurves[0] : null;

	protected override void Reset()
	{
		ResetFallbackPower();
	}

	protected override void OnChanged()
	{
		Commands.ResetFallbackPowerCommand.NotifyCanExecuteChanged();
		base.OnChanged();
	}
}

internal sealed class PowerDataPointViewModel<T> : BindableObject, IDataPoint<T, byte>
	where T : struct, INumber<T>
{
	private T _x;
	private byte _y;

	public T X
	{
		get => _x;
		set => SetValue(ref _x, value);
	}

	public byte Y
	{
		get => _y;
		set => SetValue(ref _y, value);
	}

	public PowerDataPointViewModel() { }

	public PowerDataPointViewModel(T x, byte y)
	{
		_x = x;
		_y = y;
	}
}
