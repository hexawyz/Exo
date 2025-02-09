using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Channels;
using Exo.Devices.Elgato.StreamDeck;
using Exo.Ui;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace StreamDeckPlayground;

public sealed class StreamDeckViewModel : BindableObject, IAsyncDisposable
{
	public static async Task<StreamDeckViewModel> CreateAsync(string deviceName, ushort productId, CancellationToken cancellationToken)
	{
		var device = new StreamDeckDevice(new(deviceName), productId);
		var serialNumber = await device.GetSerialNumberAsync(cancellationToken);
		var firmwareVersion = await device.GetVersionAsync(cancellationToken);
		uint usageTime = await device.GetUsageTimeAsync(cancellationToken);
		var info = await device.GetDeviceInfoAsync(cancellationToken);
		return new(deviceName, device, serialNumber, firmwareVersion, usageTime, info);
	}

	private readonly string _deviceName;
	private readonly StreamDeckDevice _device;
	private readonly StreamDeckDeviceInfo _deviceInformation;
	private readonly string _serialNumber;
	private readonly string _firmwareVersion;
	private readonly uint _usageTime;
	private readonly ReadOnlyCollection<StreamDeckButtonViewModel> _buttons;
	private StreamDeckButtonViewModel? _selectedButton;
	private readonly ChannelWriter<(byte KeyIndex, Rgb24 Color)> _updateWriter;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _executeUpdatesTask;

	public StreamDeckViewModel(string deviceName, StreamDeckDevice device, string serialNumber, string firmwareVersion, uint usageTime, StreamDeckDeviceInfo deviceInformation)
	{
		_deviceName = deviceName;
		_device = device;
		_deviceInformation = deviceInformation;
		_serialNumber = serialNumber;
		_firmwareVersion = firmwareVersion;
		_usageTime = usageTime;
		_buttons = Array.AsReadOnly(Enumerable.Range(0, deviceInformation.ButtonCount).Select(i => new StreamDeckButtonViewModel(this, (byte)i)).ToArray());
		var channel = Channel.CreateUnbounded<(byte KeyIndex, Rgb24 Color)>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });
		_updateWriter = channel;
		_cancellationTokenSource = new();
		_executeUpdatesTask = ExecuteUpdatesAsync(channel, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _executeUpdatesTask;
			cts.Dispose();
		}
	}

	private async Task ExecuteUpdatesAsync(ChannelReader<(byte KeyIndex, Rgb24 Color)> reader, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var (keyIndex, color) in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				await SetButtonColorAsync(keyIndex, color, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	public string DeviceName => _deviceName;

	public string SerialNumber => _serialNumber;
	public string FirmwareVersion => _firmwareVersion;

	public int ButtonColumnCount => _deviceInformation.ButtonColumnCount;
	public int ButtonRowCount => _deviceInformation.ButtonRowCount;

	public int ButtonImageWidth => _deviceInformation.ButtonImageWidth;
	public int ButtonImageHeight => _deviceInformation.ButtonImageHeight;

	public int ScreensaverImageWidth => _deviceInformation.ScreensaverImageWidth;
	public int ScreensaverImageHeight => _deviceInformation.ScreensaverImageHeight;

	public TimeSpan UsageTime => TimeSpan.FromHours(_usageTime);

	public ReadOnlyCollection<StreamDeckButtonViewModel> Buttons => _buttons;

	public StreamDeckButtonViewModel? SelectedButton
	{
		get => _selectedButton;
		set => SetValue(ref _selectedButton, value);
	}

	public void RequestColorUpdate(byte keyIndex, byte r, byte g, byte b)
		=> _updateWriter.TryWrite((keyIndex, new(r, g, b)));

	private async Task SetButtonColorAsync(byte keyIndex, Rgb24 color, CancellationToken cancellationToken)
	{
		using (var image = new Image<Rgb24>(_deviceInformation.ButtonImageWidth, _deviceInformation.ButtonImageHeight, color))
		using (var stream = new MemoryStream())
		{
			image.SaveAsBmp(stream, new BmpEncoder() { BitsPerPixel = BmpBitsPerPixel.Pixel24 });
			await _device.SetKeyImageDataAsync(keyIndex, stream.GetBuffer().AsMemory(0, (int)stream.Length), cancellationToken);
		}
	}
}
