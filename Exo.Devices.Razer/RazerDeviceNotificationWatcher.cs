using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Razer;

internal sealed class RazerDeviceNotificationWatcher : IAsyncDisposable
{
	private readonly HidFullDuplexStream _stream;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readTask;

	public RazerDeviceNotificationWatcher(HidFullDuplexStream stream, IRazerDeviceNotificationSink sink)
	{
		_stream = stream;
		_cancellationTokenSource = new();
		_readTask = ReadAsync(stream, sink, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _readTask.ConfigureAwait(false);
		await _stream.DisposeAsync().ConfigureAwait(false);
	}

	private static async Task ReadAsync(HidFullDuplexStream stream, IRazerDeviceNotificationSink sink, CancellationToken cancellationToken)
	{
		var buffer = MemoryMarshal.CreateFromPinnedArray(GC.AllocateUninitializedArray<byte>(16, true), 0, 16);

		while (true)
		{
			// Data is received in fixed length packets, so we expect to always receive exactly the number of bytes that the buffer can hold.
			var remaining = buffer;
			do
			{
				int count;
				try
				{
					count = await stream.ReadAsync(remaining, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (IOException ex) when (ex.HResult == unchecked((int)0x8007048f))
				{
					// Ignore ERROR_DEVICE_NOT_CONNECTED
					return;
				}

				if (count == 0)
				{
					return;
				}

				remaining = remaining.Slice(count);
			}
			while (remaining.Length != 0);

			HandleNotification(sink, buffer.Span[1..]);
		}
	}

	private static void HandleNotification(IRazerDeviceNotificationSink sink, Span<byte> span)
	{
		// This supposedly indicates a device connect notification.
		switch (span[0])
		{
		case 2:
			// This is a DPI notification. I'm not entirely sure in which condition this is sent, but it is hopefully triggered when the DPI switches are used.
			// There is nothing looking like a device ID here, though. I'm wondering how notifications for multiple devices work in that case.
			sink.OnDeviceDpiChange(1, BigEndian.ReadUInt16(span[1]), BigEndian.ReadUInt16(span[3]));
			break;
		case 9:
			// There are two parameters to this notification.
			// The second one is very likely a one-based device index, so we'll use it as such for now.
			switch (span[1])
			{
			case 2: sink.OnDeviceRemoval(span[2]); break;
			case 3: sink.OnDeviceArrival(span[2]); break;
			}
			break;
		case 12:
			// This notification seems to indicate when the device is charging or not.
			// It is sent when connected to or disconnected from the dock, even when at 100%.
			sink.OnDeviceExternalPowerChange(1, (span[1] & 1) != 0);
			break;
		default:
			break;
		}
	}
}
