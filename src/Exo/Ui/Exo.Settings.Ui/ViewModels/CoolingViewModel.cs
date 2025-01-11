using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.WinUI.Helpers;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Contracts.Ui.Settings.Cooling;
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
	private readonly Dictionary<Guid, Dictionary<Guid, ICoolingParameters>> _pendingCoolingConfigurationChanges;

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
		_pendingCoolingConfigurationChanges = new();
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
			var coolingService = await _connectionManager.GetCoolingServiceAsync(cancellationToken);
			var watchDevicesTask = WatchDevicesAsync(coolingService, cts.Token);
			var watchChangesTask = WatchConfigurationChangesAsync(coolingService, cts.Token);

			try
			{
				await Task.WhenAll(watchDevicesTask, watchChangesTask);
			}
			catch
			{
			}
		}
	}

	void IConnectedState.Reset()
	{
		_coolingDevicesById.Clear();
		_pendingDeviceInformations.Clear();
		_pendingCoolingConfigurationChanges.Clear();

		foreach (var device in _coolingDevices)
		{
			device.Dispose();
		}

		_coolingDevices.Clear();
	}

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchDevicesAsync(ICoolingService coolingService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var info in coolingService.WatchCoolingDevicesAsync(cancellationToken))
			{
				if (_coolingDevicesById.TryGetValue(info.DeviceId, out var vm))
				{
					OnDeviceChanged(vm, info);
				}
				else
				{
					if (_devicesViewModel.TryGetDevice(info.DeviceId, out var device))
					{
						_pendingCoolingConfigurationChanges.Remove(info.DeviceId, out var coolingParameters);
						OnDeviceAdded(device, info, coolingParameters);
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

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchConfigurationChangesAsync(ICoolingService coolingService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in coolingService.WatchCoolingChangesAsync(cancellationToken))
			{
				if (notification.RawValue is not { } parameters) continue;

				if (_coolingDevicesById.TryGetValue(parameters.DeviceId, out var vm))
				{
					vm.OnCoolingConfigurationChanged(parameters);
				}
				else
				{
					if (!_pendingCoolingConfigurationChanges.TryGetValue(parameters.DeviceId, out var pendingChanges))
					{
						_pendingCoolingConfigurationChanges.Add(parameters.DeviceId, pendingChanges = new());
					}
					pendingChanges.Add(parameters.CoolerId, parameters);
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
				_pendingCoolingConfigurationChanges.Remove(vm.Id, out var coolingParameters);
				OnDeviceAdded(vm, info, coolingParameters);
			}
		}
		else if (e.Action == NotifyCollectionChangedAction.Remove)
		{
			var vm = (DeviceViewModel)e.OldItems![0]!;
			if (!_pendingDeviceInformations.Remove(vm.Id))
			{
				_pendingCoolingConfigurationChanges.Remove(vm.Id);
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

	private void OnDeviceAdded(DeviceViewModel device, CoolingDeviceInformation coolingDeviceInformation, Dictionary<Guid, ICoolingParameters>? coolingParameters)
	{
		var vm = new CoolingDeviceViewModel(device, _sensorsViewModel.GetDevice(device.Id), coolingDeviceInformation, coolingParameters, this, _sensorsViewModel, _metadataService);
		_coolingDevices.Add(vm);
		_coolingDevicesById[vm.Id] = vm;
	}

	private void OnDeviceChanged(CoolingDeviceViewModel viewModel, CoolingDeviceInformation coolingDeviceInformation)
	{
		viewModel.UpdateDeviceInformation(coolingDeviceInformation, null);
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

	public Task<ICoolingService> GetCoolingServiceAsync(CancellationToken cancellationToken)
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
	private readonly CoolingViewModel _coolingViewModel;

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

	public CoolingDeviceViewModel
	(
		DeviceViewModel deviceViewModel,
		SensorDeviceViewModel? sensorDeviceViewModel,
		CoolingDeviceInformation coolingDeviceInformation,
		Dictionary<Guid, ICoolingParameters>? coolingParameters,
		CoolingViewModel coolingViewModel,
		SensorsViewModel sensorsViewModel,
		ISettingsMetadataService metadataService
	)
	{
		_deviceViewModel = deviceViewModel;
		_sensorDeviceViewModel = sensorDeviceViewModel;
		_coolingDeviceInformation = coolingDeviceInformation;
		_metadataService = metadataService;
		_coolers = new();
		_coolersById = new();
		_coolersBySensorId = new();
		_sensorsViewModel = sensorsViewModel;
		_coolingViewModel = coolingViewModel;
		_deviceViewModel.PropertyChanged += OnDeviceViewModelPropertyChanged;
		if (sensorDeviceViewModel is not null)
		{
			sensorDeviceViewModel.Sensors.CollectionChanged += OnSensorCollectionChanged;
		}
		UpdateDeviceInformation(coolingDeviceInformation, coolingParameters);
	}

	public void Dispose()
	{
		_deviceViewModel.PropertyChanged -= OnDeviceViewModelPropertyChanged;
		foreach (var cooler in _coolers)
		{
			cooler.Dispose();
		}
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

	public void UpdateDeviceInformation(CoolingDeviceInformation information, Dictionary<Guid, ICoolingParameters>? coolingParameters)
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
				vm.Dispose();
			}
		}

		// Add or update the cooling.
		// TODO: Manage the cooler order somehow? (Should be doable by adding the index in the viewmodel and inserting at the proper place)
		bool isOnline = _deviceViewModel.IsAvailable;
		foreach (var coolerInfo in information.Coolers)
		{
			var speedSensor = coolerInfo.SpeedSensorId is Guid sensorId ? _sensorDeviceViewModel?.GetSensor(sensorId) : null;

			ICoolingParameters? parameters = null;
			coolingParameters?.TryGetValue(coolerInfo.CoolerId, out parameters);

			if (!_coolersById.TryGetValue(coolerInfo.CoolerId, out var vm))
			{
				vm = new CoolerViewModel(this, coolerInfo, parameters, _coolingViewModel, _sensorsViewModel, speedSensor, _metadataService);
				if (isOnline)
				{
					vm.SetOnline(coolerInfo);
					vm.UpdateConfiguration(parameters);
				}
				_coolersById.Add(coolerInfo.CoolerId, vm);
				_coolers.Add(vm);
			}
			else
			{
				// We should not encounter a scenario where we have parameters for this cooler here.
				// If the device is known, the cooler should already be up-to-date.
				Debug.Assert(parameters is null);
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

	public void OnCoolingConfigurationChanged(ICoolingParameters? coolingParameters)
	{
		if (coolingParameters is not null && _coolersById.TryGetValue(coolingParameters.CoolerId, out var cooler))
		{
			cooler.UpdateConfiguration(coolingParameters);
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

internal sealed class CoolerViewModel : ApplicableResettableBindableObject, IDisposable
{
	public CoolingDeviceViewModel Device { get; }
	private ReadOnlyCollection<ICoolingModeViewModel> _coolingModes;

	private CoolerInformation _coolerInformation;
	private SensorViewModel? _speedSensor;
	private readonly string _coolerDisplayName;
	private bool _isExpanded;

	private ICoolingModeViewModel? _initialCoolingMode;
	private ICoolingModeViewModel? _currentCoolingMode;

	private readonly CoolingViewModel _coolingViewModel;
	private readonly SensorsViewModel _sensorsViewModel;

	private readonly FixedCoolingModeViewModel? _fixedCoolingViewModel = null;
	private readonly SoftwareControlCurveCoolingModeViewModel? _softwareCurveCoolingViewModel = null;
	private readonly HardwareControlCurveCoolingModeViewModel? _hardwareCurveCoolingViewModel = null;

	// NB: Do not change this without also changing OnCoolingModePropertyChanged, or state updates will break.
	public override bool IsChanged => _initialCoolingMode != _currentCoolingMode || _currentCoolingMode?.IsChanged == true;

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

	public CoolerViewModel
	(
		CoolingDeviceViewModel device,
		CoolerInformation coolerInformation,
		ICoolingParameters? coolingParameters,
		CoolingViewModel coolingViewModel,
		SensorsViewModel sensorsViewModel,
		SensorViewModel? speedSensor,
		ISettingsMetadataService metadataService
	)
	{
		Device = device;
		_coolerInformation = coolerInformation;
		_coolingViewModel = coolingViewModel;
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
		if ((coolerInformation.SupportedCoolingModes & RawCoolingModes.Manual) != 0)
		{
			coolingModes.Add(_fixedCoolingViewModel = CreateFixedCoolingMode(coolerInformation));
			coolingModes.Add(_softwareCurveCoolingViewModel = CreateSoftwareCurveCoolingMode(coolerInformation, sensorsViewModel));
		}
		if ((coolerInformation.SupportedCoolingModes & RawCoolingModes.HardwareControlCurve) != 0)
		{
			coolingModes.Add(_hardwareCurveCoolingViewModel = CreateHardwareCurveCoolingMode(device, coolerInformation, sensorsViewModel));
		}

		if (coolingModes.Count > 0)
		{
			_coolingModes = Array.AsReadOnly(coolingModes.ToArray());
			_currentCoolingMode = _initialCoolingMode = ApplyCoolingParameters(coolingParameters) ?? null;
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
	}

	private FixedCoolingModeViewModel CreateFixedCoolingMode(CoolerInformation coolerInformation)
	{
		var coolingMode = new FixedCoolingModeViewModel(coolerInformation.PowerLimits!.MinimumPower, coolerInformation.PowerLimits.CanSwitchOff);
		coolingMode.PropertyChanged += OnCoolingModePropertyChanged;
		return coolingMode;
	}

	private SoftwareControlCurveCoolingModeViewModel CreateSoftwareCurveCoolingMode(CoolerInformation coolerInformation, SensorsViewModel sensorsViewModel)
	{
		var coolingMode = new SoftwareControlCurveCoolingModeViewModel(sensorsViewModel, coolerInformation.PowerLimits!.MinimumPower, coolerInformation.PowerLimits.CanSwitchOff);
		coolingMode.PropertyChanged += OnCoolingModePropertyChanged;
		return coolingMode;
	}

	private HardwareControlCurveCoolingModeViewModel CreateHardwareCurveCoolingMode(CoolingDeviceViewModel device, CoolerInformation coolerInformation, SensorsViewModel sensorsViewModel)
	{
		var coolingMode = new HardwareControlCurveCoolingModeViewModel(device.Id, sensorsViewModel, coolerInformation.PowerLimits!.MinimumPower, coolerInformation.PowerLimits.CanSwitchOff);
		coolingMode.PropertyChanged += OnCoolingModePropertyChanged;
		return coolingMode;
	}

	private void OnCoolingModePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (Equals(e, ChangedProperty.IsChanged))
		{
			// This check is synchronized with the IsChanged property.
			if (ReferenceEquals(sender, _currentCoolingMode) && ReferenceEquals(_initialCoolingMode, _currentCoolingMode))
			{
				OnChanged(IsChanged);
			}
		}
	}

	public void Dispose()
	{
		foreach (var coolingMode in _coolingModes)
		{
			coolingMode.Dispose();
		}
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
			ICoolingModeViewModel? softwareControlCurveCoolingModeViewModel = null;
			ICoolingModeViewModel? hardwareControlCurveCoolingModeViewModel = null;

			foreach (var coolingMode in _coolingModes)
			{
				switch (coolingMode)
				{
				case AutomaticCoolingModeViewModel: automaticCoolingModeViewModel = coolingMode; break;
				case FixedCoolingModeViewModel: fixedCoolingModeViewModel = coolingMode; break;
				case SoftwareControlCurveCoolingModeViewModel: softwareControlCurveCoolingModeViewModel = coolingMode; break;
				case HardwareControlCurveCoolingModeViewModel: hardwareControlCurveCoolingModeViewModel = coolingMode; break;
				}
			}

			var coolingModes = new List<ICoolingModeViewModel>();
			if ((information.SupportedCoolingModes & RawCoolingModes.Automatic) != 0)
			{
				coolingModes.Add(automaticCoolingModeViewModel ?? AutomaticCoolingModeViewModel.Instance);
			}
			else if ((information.SupportedCoolingModes & RawCoolingModes.Manual) != 0)
			{
				coolingModes.Add(fixedCoolingModeViewModel ?? CreateFixedCoolingMode(information));
				coolingModes.Add(softwareControlCurveCoolingModeViewModel ?? CreateSoftwareCurveCoolingMode(information, _sensorsViewModel));
			}
			else if ((information.SupportedCoolingModes & RawCoolingModes.HardwareControlCurve) != 0)
			{
				coolingModes.Add(hardwareControlCurveCoolingModeViewModel ?? CreateHardwareCurveCoolingMode(Device, information, _sensorsViewModel));
			}
			_coolingModes = coolingModes.Count > 0 ? Array.AsReadOnly(coolingModes.ToArray()) : ReadOnlyCollection<ICoolingModeViewModel>.Empty;
			NotifyPropertyChanged(ChangedProperty.CoolingModes);
		}
		bool wasChanged = IsChanged;
		var oldInitialCoolingMode = _initialCoolingMode;
		var newInitialCoolingMode = _coolingModes.Count > 0 ? _coolingModes[0] : null;
		if (oldInitialCoolingMode is not null && !_coolingModes.Contains(oldInitialCoolingMode))
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

	public void UpdateConfiguration(ICoolingParameters? coolingParameters)
	{
		bool wasChanged = IsChanged;
		if (ApplyCoolingParameters(coolingParameters) is { } newCoolingMode)
		{
			if (_currentCoolingMode == _initialCoolingMode)
			{
				_currentCoolingMode = newCoolingMode;
			}
			_initialCoolingMode = newCoolingMode;
		}
		OnChangeStateChange(wasChanged);
	}

	private ICoolingModeViewModel? ApplyCoolingParameters(ICoolingParameters? coolingParameters)
	{
		switch (coolingParameters)
		{
		case AutomaticCoolingParameters when (_coolerInformation.SupportedCoolingModes & RawCoolingModes.Automatic) != 0:
			return AutomaticCoolingModeViewModel.Instance;
		case FixedCoolingParameters fixedCoolingParameters when _fixedCoolingViewModel is not null:
			_fixedCoolingViewModel.SetInitialPower(fixedCoolingParameters.Power);
			return _fixedCoolingViewModel;
		case SoftwareCurveCoolingParameters softwareCurveCoolingParameters when _softwareCurveCoolingViewModel is not null:
			_softwareCurveCoolingViewModel.OnCoolingConfigurationChanged(softwareCurveCoolingParameters);
			return _softwareCurveCoolingViewModel;
		case HardwareCurveCoolingParameters hardwareCurveCoolingParameters when _hardwareCurveCoolingViewModel is not null:
			_hardwareCurveCoolingViewModel.OnCoolingConfigurationChanged(hardwareCurveCoolingParameters);
			return _hardwareCurveCoolingViewModel;
		default: return null;
		}
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

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		var coolingService = await _coolingViewModel.GetCoolingServiceAsync(cancellationToken);
		if (_currentCoolingMode is not null)
		{
			await _currentCoolingMode.ApplyAsync(coolingService, Device.Id, Id, cancellationToken);
		}
	}

	protected override void Reset()
	{
		if (!IsChanged) return;

		if (!ReferenceEquals(_currentCoolingMode, _initialCoolingMode))
		{
			_currentCoolingMode = _initialCoolingMode;
			NotifyPropertyChanged(ChangedProperty.CurrentCoolingMode);
		}
		// This may fire useless events, but we need the state to be correct before calling the reset method on the cooling mode.
		OnChangeStateChange(true);
		// This can raise PropertyChanged. Unregistering event handlers or something similar would be needed if we wanted to avoid this.
		_currentCoolingMode?.Reset();
	}
}

internal enum LogicalCoolingMode
{
	Automatic = 0,
	Fixed = 1,
	SoftwareControlCurve = 2,
	HardwareControlCurve = 3,
}

internal interface ICoolingModeViewModel : IResettable, IDisposable
{
	LogicalCoolingMode CoolingMode { get; }
	ValueTask ApplyAsync(ICoolingService coolingService, Guid deviceId, Guid coolerId, CancellationToken cancellationToken);
}

internal sealed class AutomaticCoolingModeViewModel : ICoolingModeViewModel
{
	public static readonly AutomaticCoolingModeViewModel Instance = new();

	private AutomaticCoolingModeViewModel() { }
	public void Dispose() { }

	public LogicalCoolingMode CoolingMode => LogicalCoolingMode.Automatic;
	public bool IsChanged => false;
	public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
	void IResettable.Reset() { }

	public ValueTask ApplyAsync(ICoolingService coolingService, Guid deviceId, Guid coolerId, CancellationToken cancellationToken)
		=> coolingService.SetAutomaticCoolingAsync(new() { DeviceId = deviceId, CoolerId = coolerId }, cancellationToken);
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

	public void Dispose() { }

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
				OnChanged(true);
			}
		}
	}

	internal void ResetPower() => Power = _initialPower;

	protected override void Reset() => ResetPower();

	protected override void OnChanged(bool isChanged)
	{
		Commands.ResetPowerCommand.NotifyCanExecuteChanged();
		base.OnChanged(isChanged);
	}

	public ValueTask ApplyAsync(ICoolingService coolingService, Guid deviceId, Guid coolerId, CancellationToken cancellationToken)
		=> coolingService.SetFixedCoolingAsync(new() { DeviceId = deviceId, CoolerId = coolerId, Power = Power }, cancellationToken);
}

internal abstract class ControlCurveCoolingModeViewModel : ResettableBindableObject, ICoolingModeViewModel
{
	private static class Commands
	{
		public sealed class ResetInputSensorCommand : ICommand
		{
			public static readonly ResetInputSensorCommand Instance = new();

			private ResetInputSensorCommand() { }

			public void Execute(object? parameter) => ((ControlCurveCoolingModeViewModel)parameter!).ResetInputSensorAndCurve();
			public bool CanExecute(object? parameter) => (parameter as ControlCurveCoolingModeViewModel)?.IsChanged ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void NotifyCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	private static ImmutableArray<Contracts.Ui.IDataPoint<TInput, ulong>> CastDataPoints<TDataPoint, TInput>(ImmutableArray<TDataPoint> points)
		where TDataPoint : Contracts.Ui.IDataPoint<TInput, ulong>
		where TInput : struct, INumber<TInput>
		=> ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<Contracts.Ui.IDataPoint<TInput, ulong>[]>(ImmutableCollectionsMarshal.AsArray(points)));

	private static IList? CreateDataPoints(SensorDataType dataType, CoolingControlCurve curve)
		=> curve.RawValue switch
		{
			null => null,
			UnsignedIntegerCoolingControlCurve curve2 => CreateDataPoints(dataType, curve2.SegmentPoints),
			SignedIntegerCoolingControlCurve curve2 => CreateDataPoints(dataType, curve2.SegmentPoints),
			SinglePrecisionFloatingPointCoolingControlCurve curve2 => CreateDataPoints(dataType, curve2.SegmentPoints),
			DoublePrecisionFloatingPointCoolingControlCurve curve2 => CreateDataPoints(dataType, curve2.SegmentPoints),
			_ => throw new InvalidOperationException()
		};

	private static IList CreateDataPoints(SensorDataType dataType, ImmutableArray<UIntDataPoint> points)
		=> CreateDataPoints(dataType, CastDataPoints<UIntDataPoint, ulong>(points));

	private static IList CreateDataPoints(SensorDataType dataType, ImmutableArray<IntToUIntDataPoint> points)
		=> CreateDataPoints(dataType, CastDataPoints<IntToUIntDataPoint, long>(points));

	private static IList CreateDataPoints(SensorDataType dataType, ImmutableArray<SingleToUIntDataPoint> points)
		=> CreateDataPoints(dataType, CastDataPoints<SingleToUIntDataPoint, float>(points));

	private static IList CreateDataPoints(SensorDataType dataType, ImmutableArray<DoubleToUIntDataPoint> points)
		=> CreateDataPoints(dataType, CastDataPoints<DoubleToUIntDataPoint, double>(points));

	private static IList CreateDataPoints<TSrc>(SensorDataType dataType, ImmutableArray<Contracts.Ui.IDataPoint<TSrc, ulong>> points)
		where TSrc : struct, INumber<TSrc>
		=> dataType switch
		{
			SensorDataType.UInt8 => CreateDataPoints<TSrc, byte>(dataType, points),
			SensorDataType.UInt16 => CreateDataPoints<TSrc, ushort>(dataType, points),
			SensorDataType.UInt32 => CreateDataPoints<TSrc, uint>(dataType, points),
			SensorDataType.UInt64 => CreateDataPoints<TSrc, ulong>(dataType, points),
			SensorDataType.UInt128 => CreateDataPoints<TSrc, UInt128>(dataType, points),
			SensorDataType.SInt8 => CreateDataPoints<TSrc, sbyte>(dataType, points),
			SensorDataType.SInt16 => CreateDataPoints<TSrc, short>(dataType, points),
			SensorDataType.SInt32 => CreateDataPoints<TSrc, int>(dataType, points),
			SensorDataType.SInt64 => CreateDataPoints<TSrc, long>(dataType, points),
			SensorDataType.SInt128 => CreateDataPoints<TSrc, Int128>(dataType, points),
			SensorDataType.Float16 => CreateDataPoints<TSrc, Half>(dataType, points),
			SensorDataType.Float32 => CreateDataPoints<TSrc, float>(dataType, points),
			SensorDataType.Float64 => CreateDataPoints<TSrc, double>(dataType, points),
			_ => throw new InvalidOperationException(),
		};

	private static IList CreateNewDataPoints(SensorDataType dataType, ImmutableArray<double> points)
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

	private static ObservableCollection<Controls.IDataPoint<TDst, byte>> CreateDataPoints<TSrc, TDst>(SensorDataType dataType, ImmutableArray<Contracts.Ui.IDataPoint<TSrc, ulong>> points)
		where TSrc : struct, INumber<TSrc>
		where TDst : struct, INumber<TDst>
	{
		var collection = new ObservableCollection<Controls.IDataPoint<TDst, byte>>();
		foreach (var point in points)
		{
			collection.Add(new PowerDataPointViewModel<TDst>(TDst.CreateChecked(point.X), (byte)point.Y));
		}
		return collection;
	}

	private static ObservableCollection<Controls.IDataPoint<T, byte>> CreateNewDataPoints<T>(SensorDataType dataType, ImmutableArray<double> points)
		where T : struct, INumber<T>
	{
		var collection = new ObservableCollection<Controls.IDataPoint<T, byte>>();
		foreach (double value in points)
		{
			collection.Add(new PowerDataPointViewModel<T>(T.CreateChecked(value), 100));
		}
		return collection;
	}

	private static void AddPropertyChangedEventHandler(IEnumerable? items, PropertyChangedEventHandler handler)
	{
		if (items is null) return;

		foreach (INotifyPropertyChanged item in items)
		{
			item.PropertyChanged += handler;
		}
	}

	private static void RemovePropertyChangedEventHandler(IEnumerable? items, PropertyChangedEventHandler handler)
	{
		if (items is null) return;

		foreach (INotifyPropertyChanged item in items)
		{
			item.PropertyChanged += handler;
		}
	}

	private CoolingControlCurve? _initialCurve;
	private IEnumerable? _points;

	private readonly byte _minimumPower;
	private readonly bool _canSwitchOff;
	private bool _hasCurveChanged;

	public object? Points => _points;

	private readonly SensorsViewModel _sensorsViewModel;
	private Guid _initialInputSensorDeviceId;
	private Guid _initialInputSensorId;
	private Guid _currentInputSensorDeviceId;
	private Guid _currentInputSensorId;
	private SensorViewModel? _inputSensor;

	private readonly NotifyCollectionChangedEventHandler _notifyCollectionChangedEventHandler;
	private readonly PropertyChangedEventHandler _pointPropertyChangedEventHandler;

	public abstract LogicalCoolingMode CoolingMode { get; }

	public ICommand ResetInputSensorCommand => Commands.ResetInputSensorCommand.Instance;

	public byte MinimumOnPower => _minimumPower;
	public byte MinimumPower => _canSwitchOff ? (byte)0 : _minimumPower;
	public bool CanSwitchOff => _canSwitchOff;

	protected SensorsViewModel SensorsViewModel => _sensorsViewModel;

	public abstract ObservableCollection<SensorViewModel> SensorsAvailableForCoolingControlCurves { get; }

	// This property should only be used for the UI side.
	// Sensors can be loaded after the configuration has been updated, and the property will change as needed.
	// If this property is used as a reference, it could lead to incorrect results.
	public SensorViewModel? InputSensor
	{
		get => _inputSensor;
		set
		{
			// Ignoring requests to set the value to null make the code simpler and might avoid some weird data binding edge cases.
			if (value == null) return;

			bool wasChanged = IsChanged;

			if (value != _inputSensor)
			{
				var oldSensorDeviceId = _currentInputSensorDeviceId;
				var oldSensorId = _currentInputSensorId;
				var newSensorDeviceId = value.Device.Id;
				var newSensorId = value.Id;

				_currentInputSensorDeviceId = newSensorDeviceId;
				_currentInputSensorId = newSensorId;
				_inputSensor = value;

				NotifyPropertyChanged(ChangedProperty.InputSensor);

				if (newSensorDeviceId != oldSensorDeviceId || newSensorId != oldSensorId)
				{
					OnInputSensorChanged(value, newSensorDeviceId == _initialInputSensorDeviceId && newSensorId == _initialInputSensorId);
				}
				OnChangeStateChange(wasChanged);
			}
		}
	}

	protected virtual void OnInputSensorChanged(SensorViewModel sensor, bool isInitialSensor)
	{
		IEnumerable? newPoints;
		SensorDataType dataType;
		dataType = sensor.DataType;
		if (isInitialSensor && _initialCurve is not null)
		{
			newPoints = CreateDataPoints(dataType, _initialCurve);
			_hasCurveChanged = false;
		}
		else
		{
			newPoints = CreateNewDataPoints(sensor.DataType, sensor.PresetControlCurveSteps);
			_hasCurveChanged = true;
		}
		if (!AreCurvesEqual(dataType, newPoints, _points))
		{
			RemovePropertyChangedEventHandler(_points, _pointPropertyChangedEventHandler);
			_points = newPoints;
			AddPropertyChangedEventHandler(_points, _pointPropertyChangedEventHandler);
			NotifyPropertyChanged(ChangedProperty.Points);
		}
	}

	protected bool HasInputSensorChanged => _currentInputSensorDeviceId != _initialInputSensorDeviceId || _currentInputSensorId != _initialInputSensorId;
	protected bool HasCurveChanged => _hasCurveChanged;

	public override bool IsChanged => HasInputSensorChanged || HasCurveChanged;

	public ControlCurveCoolingModeViewModel(SensorsViewModel sensorsViewModel, byte minimumPower, bool canSwitchOff)
	{
		_notifyCollectionChangedEventHandler = OnSensorsAvailableForCoolingControlCurvesCollectionChanged;
		_pointPropertyChangedEventHandler = OnPointPropertyChanged;

		_sensorsViewModel = sensorsViewModel;

		_minimumPower = minimumPower;
		_canSwitchOff = canSwitchOff;

		if (_sensorsViewModel.SensorsAvailableForCoolingControlCurves.Count > 0)
		{
			_inputSensor = _sensorsViewModel.SensorsAvailableForCoolingControlCurves[0];
			_points = CreateNewDataPoints(_inputSensor.DataType, _inputSensor.PresetControlCurveSteps);
			AddPropertyChangedEventHandler(_points, _pointPropertyChangedEventHandler);
		}

		sensorsViewModel.SensorsAvailableForCoolingControlCurves.CollectionChanged += _notifyCollectionChangedEventHandler;
	}

	public virtual void Dispose()
	{
		SensorsViewModel.SensorsAvailableForCoolingControlCurves.CollectionChanged -= _notifyCollectionChangedEventHandler;
	}

	protected virtual void OnSensorsAvailableForCoolingControlCurvesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		var oldInputSensor = _inputSensor;

		switch (e.Action)
		{
		case NotifyCollectionChangedAction.Add:
			foreach (SensorViewModel sensor in e.NewItems!)
			{
				if (sensor.Device.Id == _currentInputSensorDeviceId && sensor.Id == _currentInputSensorId)
				{
					_inputSensor = sensor;
					break;
				}
			}
			break;
		case NotifyCollectionChangedAction.Remove:
			foreach (SensorViewModel sensor in e.OldItems!)
			{
				if (sensor.Device.Id == _currentInputSensorDeviceId && sensor.Id == _currentInputSensorId)
				{
					_inputSensor = null;
					break;
				}
			}
			break;
		case NotifyCollectionChangedAction.Replace:
			foreach (SensorViewModel sensor in e.OldItems!)
			{
				if (sensor.Device.Id == _currentInputSensorDeviceId && sensor.Id == _currentInputSensorId)
				{
					_inputSensor = null;
					break;
				}
			}
			foreach (SensorViewModel sensor in e.NewItems!)
			{
				if (sensor.Device.Id == _currentInputSensorDeviceId && sensor.Id == _currentInputSensorId)
				{
					_inputSensor = sensor;
					break;
				}
			}
			break;
		case NotifyCollectionChangedAction.Move:
			break;
		case NotifyCollectionChangedAction.Reset:
			_inputSensor = null;
			foreach (var sensor in SensorsViewModel.SensorsAvailableForCoolingControlCurves)
			{
				if (sensor.Device.Id == _currentInputSensorDeviceId && sensor.Id == _currentInputSensorId)
				{
					_inputSensor = sensor;
					break;
				}
			}
			break;
		}

		if (_inputSensor != oldInputSensor) NotifyPropertyChanged(ChangedProperty.InputSensor);
	}

	private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool wasChanged = IsChanged;
		_hasCurveChanged = true;
		OnChangeStateChange(wasChanged);
	}

	public void OnCoolingConfigurationChanged(ICurveCoolingParameters parameters)
	{
		bool wasChanged = IsChanged;

		var oldInitialSensorDeviceId = _initialInputSensorDeviceId;
		var oldInitialSensorId = _initialInputSensorId;
		SensorViewModel? oldSensor = _inputSensor;
		var sensorDeviceId = (parameters as SoftwareCurveCoolingParameters)?.SensorDeviceId ?? parameters.DeviceId;
		var sensorId = parameters.SensorId;
		_initialInputSensorDeviceId = sensorDeviceId;
		_initialInputSensorId = sensorId;

		// First, update the current sensor ID if necessary.
		if (
			sensorDeviceId != oldInitialSensorDeviceId || sensorId != oldInitialSensorId &&
			_currentInputSensorDeviceId == oldInitialSensorDeviceId && _currentInputSensorId == oldInitialSensorId)
		{
			_currentInputSensorDeviceId = sensorDeviceId;
			_currentInputSensorId = sensorId;
			if (sensorDeviceId != default && sensorId != default)
			{
				foreach (var sensor in SensorsAvailableForCoolingControlCurves)
				{
					if (sensor.Device.Id == sensorDeviceId && sensor.Id == sensorId)
					{
						_inputSensor = sensor;
						goto SensorUpdated;
					}
				}
			}
			_inputSensor = null;
		SensorUpdated:;
		}

		if (oldSensor != _inputSensor) NotifyPropertyChanged(ChangedProperty.InputSensor);

		OnCoolingInputSensorConfigurationChanged(parameters, _currentInputSensorDeviceId == sensorDeviceId && _currentInputSensorId == sensorId, _inputSensor != oldSensor);

		OnChangeStateChange(wasChanged);
	}

	// This method is called to process sensor-dependent value changes.
	// It MUST NOT trigger the PropertyChanged event for IsChanged. This will be handled by the caller.
	protected virtual void OnCoolingInputSensorConfigurationChanged(ICurveCoolingParameters parameters, bool isInitialSensor, bool wasInputSensorUpdated)
	{
		_initialCurve = parameters.ControlCurve;

		if (isInitialSensor)
		{
			bool pointsChanged;

			var dataType = _inputSensor?.DataType ?? SensorDataType.UInt8;
			var newPoints = CreateDataPoints(dataType, parameters.ControlCurve);
			bool arePointsDifferent = !AreCurvesEqual(dataType, _points, newPoints);

			// We switch to the new curve if the active sensor has switched or if the data points had not changed.
			if (wasInputSensorUpdated || !_hasCurveChanged)
			{
				// Avoid changing the current collection if the contents are already up-to-date. (This is better for UI performance, as it will avoid a needless refresh)
				if (pointsChanged = arePointsDifferent)
				{
					RemovePropertyChangedEventHandler(_points, _pointPropertyChangedEventHandler);
					_points = newPoints;
					AddPropertyChangedEventHandler(newPoints, _pointPropertyChangedEventHandler);
				}
				_hasCurveChanged = false;
			}
			else
			{
				// Otherwise, we update the "change status" of the curve depending on the comparison. (Main point is: It can become unchanged)
				pointsChanged = false;
				_hasCurveChanged = arePointsDifferent;
			}

			if (pointsChanged) NotifyPropertyChanged(ChangedProperty.Points);
		}
	}

	protected void ResetInputSensorAndCurve()
	{
		SensorViewModel? inputSensor = null;
		if (_initialInputSensorDeviceId != _currentInputSensorDeviceId || _initialInputSensorId != _currentInputSensorId)
		{
			foreach (var sensor in _sensorsViewModel.SensorsAvailableForCoolingControlCurves)
			{
				if (sensor.Device.Id == _initialInputSensorDeviceId && sensor.Id == _initialInputSensorId)
				{
					inputSensor = sensor;
					break;
				}
			}
			_currentInputSensorDeviceId = _initialInputSensorDeviceId;
			_currentInputSensorId = _initialInputSensorId;
			// For now it is simpler to just consider that the curve has changed even if maybe the data is up to date. (Because the sensor has changed, we expect the curve to change)
			_hasCurveChanged = true;
			if (!ReferenceEquals(_inputSensor, inputSensor))
			{
				_inputSensor = inputSensor;
				NotifyPropertyChanged(ChangedProperty.InputSensor);
			}
		}
		else
		{
			inputSensor = _inputSensor;
		}

		if (_hasCurveChanged)
		{
			// TODO: For hardware sensors, we would have all the required metadata even if sensor view model is unavailable, so we should not care that sensor VM is null.
			if (_initialCurve is null || inputSensor is null)
			{
				if (_points is not null)
				{
					RemovePropertyChangedEventHandler(_points, _pointPropertyChangedEventHandler);
					_points = null;
					NotifyPropertyChanged(ChangedProperty.Points);
				}
				_hasCurveChanged = false;
			}
			else
			{
				var newPoints = CreateDataPoints(inputSensor.DataType, _initialCurve);
				if (!AreCurvesEqual(inputSensor.DataType, newPoints, _points))
				{
					RemovePropertyChangedEventHandler(_points, _pointPropertyChangedEventHandler);
					_points = newPoints;
					AddPropertyChangedEventHandler(_points, _pointPropertyChangedEventHandler);
					NotifyPropertyChanged(ChangedProperty.Points);
				}
				_hasCurveChanged = false;
			}
		}
	}

	protected virtual void ResetCore()
	{
		ResetInputSensorAndCurve();
	}

	protected sealed override void Reset()
	{
		if (!IsChanged) return;
		ResetCore();
		OnChangeStateChange(true);
	}

	public abstract ValueTask ApplyAsync(ICoolingService coolingService, Guid deviceId, Guid coolerId, CancellationToken cancellationToken);

	protected static bool AreCurvesEqual(SensorDataType dataType, object? a, object? b)
		=> a?.GetType() == b?.GetType() &&
			dataType switch
			{
				SensorDataType.UInt8 => AreCurvesEqual<byte>(a, b),
				SensorDataType.UInt16 => AreCurvesEqual<ushort>(a, b),
				SensorDataType.UInt32 => AreCurvesEqual<uint>(a, b),
				SensorDataType.UInt64 => AreCurvesEqual<ulong>(a, b),
				SensorDataType.UInt128 => AreCurvesEqual<UInt128>(a, b),
				SensorDataType.SInt8 => AreCurvesEqual<sbyte>(a, b),
				SensorDataType.SInt16 => AreCurvesEqual<short>(a, b),
				SensorDataType.SInt32 => AreCurvesEqual<int>(a, b),
				SensorDataType.SInt64 => AreCurvesEqual<long>(a, b),
				SensorDataType.SInt128 => AreCurvesEqual<Int128>(a, b),
				SensorDataType.Float16 => AreCurvesEqual<Half>(a, b),
				SensorDataType.Float32 => AreCurvesEqual<float>(a, b),
				SensorDataType.Float64 => AreCurvesEqual<double>(a, b),
				_ => throw new InvalidOperationException(),
			};

	protected static bool AreCurvesEqual<T>(object? a, object? b)
		where T : struct, INumber<T>
		=> AreCurvesEqual((ObservableCollection<Controls.IDataPoint<T, byte>>?)a, (ObservableCollection<Controls.IDataPoint<T, byte>>?)b);

	protected static bool AreCurvesEqual<T>(ObservableCollection<Controls.IDataPoint<T, byte>>? a, ObservableCollection<Controls.IDataPoint<T, byte>>? b)
		where T : struct, INumber<T>
	{
		if (a is null)
		{
			if (b is null) goto CurvesAreTheSame;
			else goto CurvesAreDifferent;
		}
		else if (b is null || a.Count != b.Count)
		{
			goto CurvesAreDifferent;
		}

		for (int i = 0; i < a.Count; i++)
		{
			var pa = a[i];
			var pb = b[i];

			if (pa.X != pb.X || pa.Y != pb.Y) goto CurvesAreDifferent;
		}
	CurvesAreTheSame:;
		return true;

	CurvesAreDifferent:;
		return false;
	}

	protected static CoolingControlCurve CreateCoolingControlCurve(SensorDataType dataType, byte initialValue, object points)
		=> dataType switch
		{
			SensorDataType.UInt8 => CreateUnsignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<byte, byte>>)points),
			SensorDataType.UInt16 => CreateUnsignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<ushort, byte>>)points),
			SensorDataType.UInt32 => CreateUnsignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<uint, byte>>)points),
			SensorDataType.UInt64 => CreateUnsignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<ulong, byte>>)points),
			SensorDataType.UInt128 => CreateUnsignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<UInt128, byte>>)points),
			SensorDataType.SInt8 => CreateSignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<sbyte, byte>>)points),
			SensorDataType.SInt16 => CreateSignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<short, byte>>)points),
			SensorDataType.SInt32 => CreateSignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<int, byte>>)points),
			SensorDataType.SInt64 => CreateSignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<long, byte>>)points),
			SensorDataType.SInt128 => CreateSignedCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<Int128, byte>>)points),
			SensorDataType.Float16 => CreateSinglePrecisionCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<Half, byte>>)points),
			SensorDataType.Float32 => CreateSinglePrecisionCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<float, byte>>)points),
			SensorDataType.Float64 => CreateDoublePrecisionCoolingControlCurve(initialValue, (ObservableCollection<Controls.IDataPoint<double, byte>>)points),
			_ => throw new InvalidOperationException(),
		};

	private static CoolingControlCurve CreateUnsignedCoolingControlCurve<T>(byte initialValue, IReadOnlyList<Controls.IDataPoint<T, byte>> points)
		where T : struct, INumber<T>
		=> new CoolingControlCurve
		{
			UnsignedInteger = new()
			{
				InitialValue = initialValue,
				SegmentPoints = points.Select(p => new UIntDataPoint { X = ulong.CreateChecked(p.X), Y = p.Y }).ToImmutableArray()
			}
		};

	private static CoolingControlCurve CreateSignedCoolingControlCurve<T>(byte initialValue, IReadOnlyList<Controls.IDataPoint<T, byte>> points)
		where T : struct, INumber<T>
		=> new CoolingControlCurve
		{
			SignedInteger = new()
			{
				InitialValue = initialValue,
				SegmentPoints = points.Select(p => new IntToUIntDataPoint { X = long.CreateChecked(p.X), Y = p.Y }).ToImmutableArray()
			}
		};

	private static CoolingControlCurve CreateSinglePrecisionCoolingControlCurve<T>(byte initialValue, IReadOnlyList<Controls.IDataPoint<T, byte>> points)
		where T : struct, INumber<T>
		=> new CoolingControlCurve
		{
			SinglePrecisionFloatingPoint = new()
			{
				InitialValue = initialValue,
				SegmentPoints = points.Select(p => new SingleToUIntDataPoint { X = float.CreateChecked(p.X), Y = p.Y }).ToImmutableArray()
			}
		};

	private static CoolingControlCurve CreateDoublePrecisionCoolingControlCurve<T>(byte initialValue, IReadOnlyList<Controls.IDataPoint<T, byte>> points)
		where T : struct, INumber<T>
		=> new CoolingControlCurve
		{
			DoublePrecisionFloatingPoint = new()
			{
				InitialValue = initialValue,
				SegmentPoints = points.Select(p => new DoubleToUIntDataPoint { X = float.CreateChecked(p.X), Y = p.Y }).ToImmutableArray()
			}
		};
}

internal sealed class SoftwareControlCurveCoolingModeViewModel : ControlCurveCoolingModeViewModel
{
	private static class Commands
	{
		public sealed class ResetFallbackPowerCommand : ICommand
		{
			public static readonly ResetFallbackPowerCommand Instance = new();

			private ResetFallbackPowerCommand() { }

			public void Execute(object? parameter) => ((SoftwareControlCurveCoolingModeViewModel)parameter!).ResetFallbackPower();
			public bool CanExecute(object? parameter) => (parameter as ControlCurveCoolingModeViewModel)?.IsChanged ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void NotifyCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	private byte _initialFallbackPower;
	private byte _currentFallbackPower;

	public override LogicalCoolingMode CoolingMode => LogicalCoolingMode.SoftwareControlCurve;

	public ICommand ResetFallbackPowerCommand => Commands.ResetFallbackPowerCommand.Instance;

	private bool HasFallbackPowerChanged => _initialFallbackPower != _currentFallbackPower;

	public override bool IsChanged => HasCurveChanged || HasFallbackPowerChanged;

	public byte FallbackPower
	{
		get => _currentFallbackPower;
		set
		{
			if (value >= MinimumPower && value < 100 || CanSwitchOff && value == 0)
			{
				bool wasChanged = IsChanged;
				if (SetValue(ref _currentFallbackPower, value, ChangedProperty.FallbackPower))
				{
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	public SoftwareControlCurveCoolingModeViewModel(SensorsViewModel sensorsViewModel, byte minimumPower, bool canSwitchOff)
		: base(sensorsViewModel, minimumPower, canSwitchOff)
	{
		_currentFallbackPower = _initialFallbackPower = 100;
	}

	public override ObservableCollection<SensorViewModel> SensorsAvailableForCoolingControlCurves => SensorsViewModel.SensorsAvailableForCoolingControlCurves;

	protected override void OnInputSensorChanged(SensorViewModel sensor, bool isInitialSensor)
	{
		if (isInitialSensor && _currentFallbackPower != _initialFallbackPower)
		{
			_currentFallbackPower = _initialFallbackPower;
			NotifyPropertyChanged(ChangedProperty.FallbackPower);
		}
		base.OnInputSensorChanged(sensor, isInitialSensor);
	}

	protected override void OnCoolingInputSensorConfigurationChanged(ICurveCoolingParameters parameters, bool isInitialSensor, bool wasInputSensorUpdated)
	{
		byte oldInitialFallbackPower = _initialFallbackPower;
		byte oldFallbackPower = _currentFallbackPower;
		_initialFallbackPower = ((SoftwareCurveCoolingParameters)parameters).FallbackValue;

		if (isInitialSensor)
		{
			if (wasInputSensorUpdated || _currentFallbackPower == oldInitialFallbackPower)
			{
				_currentFallbackPower = _initialFallbackPower;
			}
		}

		if (_currentFallbackPower != oldFallbackPower) NotifyPropertyChanged(ChangedProperty.FallbackPower);

		base.OnCoolingInputSensorConfigurationChanged(parameters, isInitialSensor, wasInputSensorUpdated);
	}

	public override ValueTask ApplyAsync(ICoolingService coolingService, Guid deviceId, Guid coolerId, CancellationToken cancellationToken)
	{
		if (InputSensor is null || Points is null || ((ICollection)Points).Count == 0) return ValueTask.CompletedTask;

		return coolingService.SetSoftwareControlCurveCoolingAsync
		(
			new()
			{
				CoolingDeviceId = deviceId,
				CoolerId = coolerId,
				SensorDeviceId = InputSensor.Device.Id,
				SensorId = InputSensor.Id,
				FallbackValue = FallbackPower,
				ControlCurve = CreateCoolingControlCurve(InputSensor.DataType, CanSwitchOff ? (byte)0 : MinimumPower, Points),
			},
			cancellationToken
		);
	}

	internal void SetInitialFallbackPower(byte value)
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
				OnChanged(true);
			}
		}
	}

	internal void ResetFallbackPower() => FallbackPower = _initialFallbackPower;

	protected override void ResetCore()
	{
		ResetFallbackPower();
		ResetInputSensorAndCurve();
	}

	protected override void OnChanged(bool isChanged)
	{
		Commands.ResetFallbackPowerCommand.NotifyCanExecuteChanged();
		base.OnChanged(isChanged);
	}
}

internal sealed class HardwareControlCurveCoolingModeViewModel : ControlCurveCoolingModeViewModel
{
	public override LogicalCoolingMode CoolingMode => LogicalCoolingMode.HardwareControlCurve;

	private readonly ObservableCollection<SensorViewModel> _sensorsAvailableForCoolingControlCurves;
	private readonly Guid _deviceId;

	public HardwareControlCurveCoolingModeViewModel(Guid deviceId, SensorsViewModel sensorsViewModel, byte minimumPower, bool canSwitchOff)
		: base(sensorsViewModel, minimumPower, canSwitchOff)
	{
		_deviceId = deviceId;

		_sensorsAvailableForCoolingControlCurves = new();
		ResetAvailableSensors();
	}

	// NB: This method should not be called before the constructor has completed because all code will run on the UI thread.
	// Otherwise, we would need to worry about event concurrency.
	protected override void OnSensorsAvailableForCoolingControlCurvesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		base.OnSensorsAvailableForCoolingControlCurvesCollectionChanged(sender, e);

		switch (e.Action)
		{
		case NotifyCollectionChangedAction.Add:
			foreach (SensorViewModel item in e.NewItems!)
			{
				if (item.Device.Id == _deviceId)
				{
					_sensorsAvailableForCoolingControlCurves.Add(item);
				}
			}
			break;
		case NotifyCollectionChangedAction.Remove:
			foreach (SensorViewModel item in e.OldItems!)
			{
				if (item.Device.Id == _deviceId)
				{
					_sensorsAvailableForCoolingControlCurves.Remove(item);
				}
			}
			break;
		case NotifyCollectionChangedAction.Replace:
			var removedItem = (SensorViewModel)e.OldItems![0]!;
			var addedItem = (SensorViewModel)e.NewItems![0]!;
			if (removedItem.Device.Id == _deviceId)
			{
				int removedIndex = _sensorsAvailableForCoolingControlCurves.IndexOf(removedItem);
				if (removedIndex >= 0)
				{
					if (addedItem.Device.Id == _deviceId)
					{
						_sensorsAvailableForCoolingControlCurves[removedIndex] = addedItem;
					}
					else
					{
						_sensorsAvailableForCoolingControlCurves.RemoveAt(removedIndex);
					}
				}
			}
			else if (addedItem.Device.Id == _deviceId)
			{
				_sensorsAvailableForCoolingControlCurves.Add(addedItem);
			}
			break;
		case NotifyCollectionChangedAction.Move:
			break;
		case NotifyCollectionChangedAction.Reset:
			ResetAvailableSensors();
			break;
		}
	}

	private void ResetAvailableSensors()
	{
		_sensorsAvailableForCoolingControlCurves.Clear();
		foreach (var sensor in SensorsViewModel.SensorsAvailableForCoolingControlCurves)
		{
			if (sensor.Device.Id == _deviceId)
			{
				_sensorsAvailableForCoolingControlCurves.Add(sensor);
			}
		}
	}

	public override ObservableCollection<SensorViewModel> SensorsAvailableForCoolingControlCurves => _sensorsAvailableForCoolingControlCurves;

	public override ValueTask ApplyAsync(ICoolingService coolingService, Guid deviceId, Guid coolerId, CancellationToken cancellationToken)
	{
		if (InputSensor is null || Points is null || ((ICollection)Points).Count == 0) return ValueTask.CompletedTask;

		return coolingService.SetHardwareControlCurveCoolingAsync
		(
			new()
			{
				CoolingDeviceId = deviceId,
				CoolerId = coolerId,
				SensorId = InputSensor.Id,
				ControlCurve = CreateCoolingControlCurve(InputSensor.DataType, CanSwitchOff ? (byte)0 : MinimumPower, Points),
			},
			cancellationToken
		);
	}
}
