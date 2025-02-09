using System.Collections.ObjectModel;
using DeviceTools;
using Exo.Ui;

namespace StreamDeckPlayground;

public sealed class MainViewModel : BindableObject, IAsyncDisposable
{
	private readonly ObservableCollection<StreamDeckViewModel> _devices;
	private readonly ReadOnlyObservableCollection<StreamDeckViewModel> _readOnlyDevices;
	private StreamDeckViewModel? _selectedDevice;
	private readonly Task _watchDevicesTask;
	private CancellationTokenSource? _cancellationTokenSource;

	public MainViewModel()
	{
		_devices = new();
		_readOnlyDevices = new(_devices);
		_cancellationTokenSource = new();
		_watchDevicesTask = WatchDevicesAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _watchDevicesTask;
			cts.Dispose();
		}
	}

	public StreamDeckViewModel? SelectedDevice
	{
		get => _selectedDevice;
		set => SetValue(ref _selectedDevice, value);
	}

	public async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach
			(
				var device in DeviceQuery.EnumerateAllAsync
				(
					DeviceObjectKind.DeviceInterface,
					Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid &
						Properties.System.DeviceInterface.Hid.VendorId == 0x0FD9 &
						Properties.System.DeviceInterface.Hid.ProductId == 0x006C &
						Properties.System.Devices.InterfaceEnabled == true,
					cancellationToken
				)
			)
			{
				_devices.Add(await StreamDeckViewModel.CreateAsync(device.Id, 0x006C, cancellationToken));
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	public ReadOnlyObservableCollection<StreamDeckViewModel> Devices => _readOnlyDevices;
}
