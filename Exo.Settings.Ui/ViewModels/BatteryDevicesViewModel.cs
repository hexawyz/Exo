using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class BatteryDevicesViewModel : IDisposable
{
	private readonly DevicesViewModel _devicesViewModel;
	private readonly HashSet<DeviceViewModel> _knownDevices;
	private readonly HashSet<DeviceViewModel> _availableDevices;
	public ObservableCollection<DeviceViewModel> ConnectedBatteryDevices { get; }

	public BatteryDevicesViewModel(DevicesViewModel devicesViewModel)
	{
		ArgumentNullException.ThrowIfNull(devicesViewModel);
		_devicesViewModel = devicesViewModel;
		_knownDevices = new();
		_availableDevices = new();
		ConnectedBatteryDevices = new();
		OnDeviceCollectionChanged(_devicesViewModel.Devices, new(NotifyCollectionChangedAction.Reset));
		_devicesViewModel.Devices.CollectionChanged += OnDeviceCollectionChanged;
	}

	public void Dispose()
	{
		_devicesViewModel.Devices.CollectionChanged -= OnDeviceCollectionChanged;
	}

	private void OnDeviceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
		case NotifyCollectionChangedAction.Add:
			if (e.NewItems is { } newItems)
			{
				foreach (DeviceViewModel device in newItems)
				{
					if (device.PowerFeatures is { } powerFeatures)
					{
						if (_knownDevices.Add(device))
						{
							device.PropertyChanged += OnDevicePropertyChanged;
							powerFeatures.PropertyChanged += OnDevicePowerPropertyChanged;
							if (device.IsAvailable && powerFeatures.BatteryState is not null && _availableDevices.Add(device))
							{
								ConnectedBatteryDevices.Add(device);
							}
						}
					}
				}
			}
			break;
		case NotifyCollectionChangedAction.Remove:
			if (e.OldItems is { } oldItems)
			{
				foreach (DeviceViewModel device in oldItems)
				{
					if (_knownDevices.Remove(device))
					{
						device.PropertyChanged += OnDevicePropertyChanged;
						if (device.PowerFeatures is { } powerFeatures) powerFeatures.PropertyChanged -= OnDevicePowerPropertyChanged;
						if (_availableDevices.Remove(device))
						{
							ConnectedBatteryDevices.Remove(device);
						}
					}
				}
			}
			break;
		case NotifyCollectionChangedAction.Move:
			break;
		case NotifyCollectionChangedAction.Reset:
			foreach (var device in _knownDevices)
			{
				device.PropertyChanged += OnDevicePropertyChanged;
				if (device.PowerFeatures is { } powerFeatures) powerFeatures.PropertyChanged -= OnDevicePowerPropertyChanged;
			}
			_knownDevices.Clear();
			_availableDevices.Clear();
			ConnectedBatteryDevices.Clear();
			if (sender is ObservableCollection<DeviceViewModel> collection)
			{
				foreach (var device in collection)
				{
					if (_knownDevices.Add(device))
					{
						device.PropertyChanged += OnDevicePropertyChanged;
						if (device.IsAvailable && device.PowerFeatures is not null && device.PowerFeatures.BatteryState is not null && _availableDevices.Add(device))
						{
							ConnectedBatteryDevices.Add(device);
						}
					}
				}
			}
			break;
		default:
			throw new InvalidOperationException();
		}
	}

	private void OnDevicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (sender is DeviceViewModel device && BindableObject.Equals(e, ChangedProperty.IsAvailable))
		{
			if (device.IsAvailable && device.PowerFeatures?.BatteryState is not null)
			{
				if (_availableDevices.Add(device))
				{
					ConnectedBatteryDevices.Add(device);
				}
			}
			else if (_availableDevices.Remove(device))
			{
				ConnectedBatteryDevices.Remove(device);
			}
		}
	}

	private void OnDevicePowerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (sender is PowerFeaturesViewModel powerFeatures && BindableObject.Equals(e, ChangedProperty.BatteryState))
		{
			var device = powerFeatures.Device;
			if (device.IsAvailable && powerFeatures.BatteryState is not null)
			{
				if (_availableDevices.Add(device))
				{
					ConnectedBatteryDevices.Add(device);
				}
			}
			else if (_availableDevices.Remove(device))
			{
				ConnectedBatteryDevices.Remove(device);
			}
		}
	}
}
