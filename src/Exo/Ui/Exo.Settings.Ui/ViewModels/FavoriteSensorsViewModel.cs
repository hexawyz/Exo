using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class FavoriteSensorsViewModel : IDisposable
{
	private readonly SensorsViewModel _sensorsViewModel;
	private readonly Dictionary<SensorDeviceViewModel, DeviceState> _knownDevices;
	private readonly ObservableCollection<SensorViewModel> _connectedSensors;
	public ReadOnlyObservableCollection<SensorViewModel> ConnectedSensors { get; }

	private sealed class DeviceState : IDisposable
	{
		private readonly FavoriteSensorsViewModel _owner;
		private readonly SensorDeviceViewModel _device;
		private readonly HashSet<SensorViewModel> _knownSensors;
		private bool _isAvailable;

		public DeviceState(FavoriteSensorsViewModel owner, SensorDeviceViewModel device)
		{
			_owner = owner;
			_device = device;
			_knownSensors = [.. _device.Sensors];
			if (_device.IsAvailable) SetOnline();
			else SetOffline();
			device.PropertyChanged += OnDevicePropertyChanged;
			((INotifyCollectionChanged)device.Sensors).CollectionChanged += OnSensorsCollectionChanged;
		}

		public void Dispose()
		{
			((INotifyCollectionChanged)_device.Sensors).CollectionChanged -= OnSensorsCollectionChanged;
			_device.PropertyChanged -= OnDevicePropertyChanged;

			SetOffline();
		}

		private void OnDevicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (sender is SensorDeviceViewModel device && BindableObject.Equals(e, ChangedProperty.IsAvailable))
			{
				if (device.IsAvailable)
				{
					if (!_isAvailable)
					{
						SetOnline();
					}
				}
				else if (_isAvailable)
				{
					SetOffline();
				}
			}
		}

		private void OnSensorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (sender is SensorViewModel sensor && BindableObject.Equals(e, ChangedProperty.IsFavorite) && _isAvailable)
			{
				if (sensor.IsFavorite)
				{
					_owner._connectedSensors.Add(sensor);
				}
				else if (_isAvailable)
				{
					_owner._connectedSensors.Remove(sensor);
				}
			}
		}

		private void SetOnline()
		{
			_isAvailable = true;
			foreach (var sensor in _knownSensors)
			{
				if (sensor.IsFavorite)
				{
					_owner._connectedSensors.Add(sensor);
				}
				sensor.PropertyChanged += OnSensorPropertyChanged;
			}
		}

		private void SetOffline()
		{
			_isAvailable = false;
			foreach (var sensor in _knownSensors)
			{
				sensor.PropertyChanged -= OnSensorPropertyChanged;
				_owner._connectedSensors.Remove(sensor);
			}
		}

		private void OnSensorsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems is { } newItems)
					{
						foreach (SensorViewModel sensor in newItems)
						{
							if (_knownSensors.Add(sensor) && _isAvailable)
							{
								if (sensor.IsFavorite)
								{
									_owner._connectedSensors.Add(sensor);
								}
								sensor.PropertyChanged += OnSensorPropertyChanged;
							}
						}
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					if (e.OldItems is { } oldItems)
					{
						foreach (SensorViewModel sensor in oldItems)
						{
							if (_knownSensors.Remove(sensor) && _isAvailable)
							{
								sensor.PropertyChanged -= OnSensorPropertyChanged;
								if (sensor.IsFavorite)
								{
									_owner._connectedSensors.Remove(sensor);
								}
							}
						}
					}
					break;
				case NotifyCollectionChangedAction.Move:
					break;
				case NotifyCollectionChangedAction.Reset:
					if (_isAvailable)
					{
						foreach (var sensor in _knownSensors)
						{
							sensor.PropertyChanged -= OnSensorPropertyChanged;
							_owner._connectedSensors.Remove(sensor);
						}
					}
					_knownSensors.Clear();
					if (sender is ObservableCollection<SensorViewModel> collection)
					{
						foreach (var sensor in collection)
						{
							if (_knownSensors.Add(sensor) && _isAvailable)
							{
								if (sensor.IsFavorite)
								{
									_owner._connectedSensors.Add(sensor);
								}
								sensor.PropertyChanged += OnSensorPropertyChanged;
							}
						}
					}
					break;
				default:
					throw new InvalidOperationException();
			}
		}
	}

	public FavoriteSensorsViewModel(SensorsViewModel sensorsViewModel)
	{
		ArgumentNullException.ThrowIfNull(sensorsViewModel);
		_sensorsViewModel = sensorsViewModel;
		_knownDevices = new();
		_connectedSensors = new();
		ConnectedSensors = new(_connectedSensors);
		OnDeviceCollectionChanged(_sensorsViewModel.Devices, new(NotifyCollectionChangedAction.Reset));
		_sensorsViewModel.Devices.CollectionChanged += OnDeviceCollectionChanged;
	}

	public void Dispose()
	{
		_sensorsViewModel.Devices.CollectionChanged -= OnDeviceCollectionChanged;
	}

	private void OnDeviceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				if (e.NewItems is { } newItems)
				{
					foreach (SensorDeviceViewModel device in newItems)
					{
						_knownDevices.Add(device, new(this, device));
					}
				}
				break;
			case NotifyCollectionChangedAction.Remove:
				if (e.OldItems is { } oldItems)
				{
					foreach (SensorDeviceViewModel device in oldItems)
					{
						if (_knownDevices.Remove(device, out var state))
						{
							state.Dispose();
						}
					}
				}
				break;
			case NotifyCollectionChangedAction.Move:
				break;
			case NotifyCollectionChangedAction.Reset:
				// Clearing the sensors first will speed up the state dispose operations, as they will not have to look over the whole collection to remove their lights.
				_connectedSensors.Clear();
				foreach (var state in _knownDevices.Values)
				{
					state.Dispose();
				}
				_knownDevices.Clear();
				if (sender is ObservableCollection<SensorDeviceViewModel> collection)
				{
					foreach (var device in collection)
					{
						_knownDevices.Add(device, new(this, device));
					}
				}
				break;
			default:
				throw new InvalidOperationException();
		}
	}
}
