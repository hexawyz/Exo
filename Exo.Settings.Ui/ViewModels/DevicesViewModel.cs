using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Converters;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DevicesViewModel : BindableObject, IAsyncDisposable, IConnectedState
{
	private static class Commands
	{
		public class NavigateToDeviceCommand : ICommand
		{
			private readonly ICommand _rootNavigationCommand;

			public NavigateToDeviceCommand(ICommand rootNavigationCommand) => _rootNavigationCommand = rootNavigationCommand;

			public void Execute(object? parameter)
			{
				ArgumentNullException.ThrowIfNull(parameter);
				if (parameter is DeviceViewModel device)
				{
					_rootNavigationCommand.Execute(new PageViewModel("Device", device.FriendlyName, DeviceCategoryToGlyphConverter.GetGlyph(device.Category), device.Id));
				}
			}

			public bool CanExecute(object? parameter) => parameter is DeviceViewModel;

			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
		}
	}

	private readonly ObservableCollection<DeviceViewModel> _devices;

	// All removed device IDs are definitely removed. Device removal is a manual request from the user, and can only happen when the device is disconnected.
	// If the same device is reconnected later, it will be considered a new device and get a new and device ID.
	private readonly HashSet<Guid> _removedDeviceIds;

	private readonly SettingsServiceConnectionManager _connectionManager;

	// Processing asynchronous status updates requires accessing the view model from the device ID.
	private readonly Dictionary<Guid, DeviceViewModel> _devicesById;

	// Used to store battery changes when the device view model is not accessible.
	private readonly Dictionary<Guid, BatteryStateViewModel> _pendingBatteryChanges;

	// Used to store DPI changes when the device view model is not accessible.
	private readonly Dictionary<Guid, DpiViewModel> _pendingDpiChanges;

	// Used to store monitor informations when the device view model is not accessible.
	private readonly Dictionary<Guid, MonitorInformation> _pendingMonitorInformations;

	// Used to store monitor setting changes when the device view model is not accessible.
	private readonly Dictionary<Guid, List<MonitorSettingValue>> _pendingMonitorSettingChanges;

	// The selected device is the device currently being observed.
	private DeviceViewModel? _selectedDevice;

	private readonly ISettingsMetadataService _metadataService;

	private readonly Commands.NavigateToDeviceCommand _navigateToDeviceCommand;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public DevicesViewModel(SettingsServiceConnectionManager connectionManager, ISettingsMetadataService metadataService, ICommand navigateCommand)
	{
		_devices = new();
		_removedDeviceIds = new();
		_connectionManager = connectionManager;
		_devicesById = new();
		_pendingBatteryChanges = new();
		_pendingDpiChanges = new();
		_pendingMonitorInformations = new();
		_pendingMonitorSettingChanges = new();
		_metadataService = metadataService;
		_navigateToDeviceCommand = new(navigateCommand);
		_cancellationTokenSource = new CancellationTokenSource();
		_stateRegistration = _connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		_stateRegistration.Dispose();
		_cancellationTokenSource.Cancel();
		return ValueTask.CompletedTask;
	}

	public ICommand NavigateToDeviceCommand => _navigateToDeviceCommand;

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource.IsCancellationRequested) return;
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			var deviceWatchTask = WatchDevicesAsync(cts.Token);
			var batteryWatchTask = WatchBatteryChangesAsync(cts.Token);
			var dpiWatchTask = WatchDpiChangesAsync(cts.Token);
			var monitorWatchTask = WatchMonitorsAsync(cts.Token);
			var monitorSettingWatchTask = WatchMonitorSettingChangesAsync(cts.Token);

			try
			{
				await Task.WhenAll([deviceWatchTask, batteryWatchTask, dpiWatchTask, monitorWatchTask, monitorSettingWatchTask]).ConfigureAwait(false);
			}
			catch (Exception)
			{
			}
		}
	}

	void IConnectedState.Reset()
	{
		_removedDeviceIds.Clear();
		_devicesById.Clear();
		_pendingBatteryChanges.Clear();
		_pendingDpiChanges.Clear();
		_pendingMonitorSettingChanges.Clear();

		SelectedDevice = null;

		foreach (var device in _devices)
		{
			device.IsAvailable = false;
		}

		_devices.Clear();
	}

	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var deviceService = await _connectionManager.GetDeviceServiceAsync(cancellationToken);
			await foreach (var notification in deviceService.WatchDevicesAsync(cancellationToken))
			{
				var id = notification.Details.Id;

				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					{
						//ExtendedDeviceInformation extendedDeviceInformation;
						//try
						//{
						//	extendedDeviceInformation = await _deviceService.GetExtendedDeviceInformationAsync(new() { Id = notification.Details.Id }, cancellationToken);
						//}
						//catch (Exception ex) when (ex is not OperationCanceledException)
						//{
						//	// Exceptions here would likely be caused by a driver removal.
						//	// Disconnection from the service is not yet handled.
						//	continue;
						//}
						var device = new DeviceViewModel(_connectionManager, _metadataService, notification.Details);
						await HandleDeviceArrivalAsync(device, cancellationToken);
						_devicesById.Add(notification.Details.Id, device);
						_devices.Add(device);
					}
					break;
				case WatchNotificationKind.Removal:
					_removedDeviceIds.Add(id);
					for (int i = 0; i < _devices.Count; i++)
					{
						var device = _devices[i];
						if (device.Id == id)
						{
							_devices.RemoveAt(i);
							_devicesById.Remove(id);
							HandleDeviceRemoval(device);
							break;
						}
					}
					break;
				case WatchNotificationKind.Update:
					for (int i = 0; i < _devices.Count; i++)
					{
						var device = _devices[i];
						if (device.Id == notification.Details.Id)
						{
							device.FriendlyName = notification.Details.FriendlyName;
							device.Category = notification.Details.Category;
							if (notification.Details.IsAvailable != device.IsAvailable)
							{
								if (device.IsAvailable = notification.Details.IsAvailable)
								{
									await HandleDeviceArrivalAsync(device, cancellationToken);
								}
								else
								{
									HandleDeviceRemoval(device);
								}
							}
							device.UpdateDeviceIds(notification.Details.DeviceIds, notification.Details.MainDeviceIdIndex);
							device.SerialNumber = notification.Details.SerialNumber;
							break;
						}
					}
					break;
				}
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch
		{
		}
	}

	private async Task HandleDeviceArrivalAsync(DeviceViewModel device, CancellationToken cancellationToken)
	{
		if (_pendingBatteryChanges.Remove(device.Id, out var batteryStatus))
		{
			device.BatteryState = batteryStatus;
		}
		if (_pendingDpiChanges.Remove(device.Id, out var dpi))
		{
			if (device.MouseFeatures is { } mouseFeatures)
			{
				mouseFeatures.CurrentDpi = dpi;
			}
		}
		if (_pendingMonitorInformations.Remove(device.Id, out var monitorInformation))
		{
			if (device.MonitorFeatures is { } monitorFeatures)
			{
				await monitorFeatures.UpdateInformationAsync(monitorInformation, cancellationToken);
			}
		}
		if (_pendingMonitorSettingChanges.Remove(device.Id, out var monitorSettings))
		{
			if (device.MonitorFeatures is { } monitorFeatures)
			{
				foreach (var setting in monitorSettings)
				{
					monitorFeatures.UpdateSetting(setting);
				}
			}
		}
	}

	private void HandleDeviceRemoval(DeviceViewModel device)
	{
		device.BatteryState = null;
		_pendingBatteryChanges.Remove(device.Id, out _);
	}

	private async Task WatchBatteryChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var deviceService = await _connectionManager.GetDeviceServiceAsync(cancellationToken);
			await foreach (var notification in deviceService.WatchBatteryChangesAsync(cancellationToken))
			{
				var status = new BatteryStateViewModel(notification);
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					device.BatteryState = status;
				}
				else
				{
					_pendingBatteryChanges[notification.DeviceId] = status;
				}
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception)
		{
		}
	}

	private async Task WatchDpiChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var mouseService = await _connectionManager.GetMouseServiceAsync(cancellationToken);
			await foreach (var notification in mouseService.WatchDpiChangesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MouseFeatures is { } mouseFeatures)
					{
						mouseFeatures.CurrentDpi = new(notification.Dpi);
					}
				}
				else
				{
					_pendingDpiChanges.Add(notification.DeviceId, new(notification.Dpi));
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMonitorsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var monitorService = await _connectionManager.GetMonitorServiceAsync(cancellationToken);
			await foreach (var notification in monitorService.WatchMonitorsAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MonitorFeatures is { } monitorFeatures)
					{
						await monitorFeatures.UpdateInformationAsync(notification, cancellationToken);
					}
				}
				else
				{
					_pendingMonitorInformations[notification.DeviceId] = notification;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMonitorSettingChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var monitorService = await _connectionManager.GetMonitorServiceAsync(cancellationToken);
			await foreach (var notification in monitorService.WatchSettingsAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MonitorFeatures is { } monitorFeatures)
					{
						monitorFeatures.UpdateSetting(notification);
					}
				}
				else
				{
					if (!_pendingMonitorSettingChanges.TryGetValue(notification.DeviceId, out var changes))
					{
						_pendingMonitorSettingChanges.Add(notification.DeviceId, changes = new());
					}
					changes.Add(notification);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	public ObservableCollection<DeviceViewModel> Devices => _devices;

	public DeviceViewModel? SelectedDevice
	{
		get => _selectedDevice;
		set => SetValue(ref _selectedDevice, value);
	}

	// If the ID is marked as removed, it means that it will "never" be used again for a device.
	public bool IsRemovedId(Guid id) => _removedDeviceIds.Contains(id);

	public bool TryGetDevice(Guid id, [NotNullWhen(true)] out DeviceViewModel? device)
		=> _devicesById.TryGetValue(id, out device);
}
