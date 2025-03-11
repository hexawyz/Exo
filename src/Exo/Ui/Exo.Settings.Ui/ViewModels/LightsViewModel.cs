using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightsViewModel : IDisposable
{
	private readonly DevicesViewModel _devicesViewModel;
	private readonly Dictionary<DeviceViewModel, DeviceState> _knownDevices;
	public ObservableCollection<LightViewModel> ConnectedLights { get; }

	private sealed class DeviceState : IDisposable
	{
		private readonly LightsViewModel _owner;
		private readonly DeviceViewModel _device;
		private readonly HashSet<LightViewModel> _knownLights;
		private bool _isAvailable;

		public DeviceState(LightsViewModel owner, DeviceViewModel device)
		{
			_owner = owner;
			_device = device;
			_knownLights = new();
			if (_device.IsAvailable) SetOnline();
			else SetOffline();
			device.PropertyChanged += OnPropertyChanged;
			((INotifyCollectionChanged)device.LightFeatures!.Lights).CollectionChanged += OnLightsCollectionChanged;
		}

		public void Dispose()
		{
			((INotifyCollectionChanged)_device.LightFeatures!.Lights).CollectionChanged -= OnLightsCollectionChanged;
			_device.PropertyChanged -= OnPropertyChanged;

			foreach (var light in _device.LightFeatures.Lights)
			{
				_owner.ConnectedLights.Remove(light);
			}
		}

		private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (sender is DeviceViewModel device && BindableObject.Equals(e, ChangedProperty.IsAvailable))
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

		private void SetOnline()
		{
			_isAvailable = true;
			foreach (var light in _knownLights)
			{
				_owner.ConnectedLights.Add(light);
			}
		}

		private void SetOffline()
		{
			_isAvailable = false;
			foreach (var light in _knownLights)
			{
				_owner.ConnectedLights.Remove(light);
			}
		}

		private void OnLightsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
			case NotifyCollectionChangedAction.Add:
				if (e.NewItems is { } newItems)
				{
					foreach (LightViewModel light in newItems)
					{
						if (_knownLights.Add(light) && _isAvailable)
						{
							_owner.ConnectedLights.Add(light);
						}
					}
				}
				break;
			case NotifyCollectionChangedAction.Remove:
				if (e.OldItems is { } oldItems)
				{
					foreach (LightViewModel light in oldItems)
					{
						if (_knownLights.Remove(light) && _isAvailable)
						{
							_owner.ConnectedLights.Remove(light);
						}
					}
				}
				break;
			case NotifyCollectionChangedAction.Move:
				break;
			case NotifyCollectionChangedAction.Reset:
				foreach (var light in _knownLights)
				{
					_owner.ConnectedLights.Remove(light);
				}
				_knownLights.Clear();
				if (sender is ObservableCollection<LightViewModel> collection)
				{
					foreach (var light in collection)
					{
						if (_knownLights.Add(light) && _isAvailable)
						{
							_owner.ConnectedLights.Add(light);
						}
					}
				}
				break;
			default:
				throw new InvalidOperationException();
			}
		}
	}

	public LightsViewModel(DevicesViewModel devicesViewModel)
	{
		ArgumentNullException.ThrowIfNull(devicesViewModel);
		_devicesViewModel = devicesViewModel;
		_knownDevices = new();
		ConnectedLights = new();
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
					if (device.LightFeatures is { } lightFeatures)
					{
						_knownDevices.Add(device, new(this, device));
					}
				}
			}
			break;
		case NotifyCollectionChangedAction.Remove:
			if (e.OldItems is { } oldItems)
			{
				foreach (DeviceViewModel device in oldItems)
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
			// Clearing the lights first will speed up the state dispose operations, as they will not have to look over the whole collection to remove their lights.
			ConnectedLights.Clear();
			foreach (var state in _knownDevices.Values)
			{
				state.Dispose();
			}
			_knownDevices.Clear();
			if (sender is ObservableCollection<DeviceViewModel> collection)
			{
				foreach (var device in collection)
				{
					if (device.LightFeatures is { } lightFeatures)
					{
						_knownDevices.Add(device, new(this, device));
					}
				}
			}
			break;
		default:
			throw new InvalidOperationException();
		}
	}
}
