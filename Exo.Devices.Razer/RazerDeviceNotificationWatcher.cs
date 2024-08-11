using System.Runtime.InteropServices;
using DeviceTools;

namespace Exo.Devices.Razer;

internal sealed class RazerDeviceNotificationWatcher : IAsyncDisposable
{
	private readonly DeviceStream _stream;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readTask;

	public RazerDeviceNotificationWatcher(DeviceStream stream, IRazerDeviceNotificationSink sink, DeviceNotificationOptions deviceNotificationOptions)
	{
		_stream = stream;
		_cancellationTokenSource = new();
		var buffer = MemoryMarshal.CreateFromPinnedArray(GC.AllocateUninitializedArray<byte>(deviceNotificationOptions.ReportLength, true), 0, deviceNotificationOptions.ReportLength);
		_readTask = ReadAsync(stream, sink, buffer, deviceNotificationOptions, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _readTask.ConfigureAwait(false);
		await _stream.DisposeAsync().ConfigureAwait(false);
	}

	private static async Task ReadAsync(DeviceStream stream, IRazerDeviceNotificationSink sink, Memory<byte> buffer, DeviceNotificationOptions options, CancellationToken cancellationToken)
	{
		while (true)
		{
			// We should (hopefully) receive data in complete packets, as they are exposed on the wire.
			int count;
			try
			{
				count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
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

			HandleNotification(sink, buffer.Span, options);
		}
	}

	private static void HandleNotification(IRazerDeviceNotificationSink sink, ReadOnlySpan<byte> span, DeviceNotificationOptions options)
	{
		// For now, silently ignore potentially invalid data.
		// We might want to log this in the future so that bugs can be detected more easily.
		if (span[0] == options.ReportId)
		{
			if (!options.HasBluetoothHidQuirk)
			{
				HandleNotification(sink, span[1..], options.StreamIndex);
			}
			else if (span[1] == options.ReportId)
			{
				HandleNotification(sink, span[2..], options.StreamIndex);
			}
		}
	}

	private static void HandleNotification(IRazerDeviceNotificationSink sink, ReadOnlySpan<byte> span, byte streamIndex)
	{
		// This supposedly indicates a device connect notification.
		switch (span[0])
		{
		case 0x02:
			// This is a DPI notification. I'm not entirely sure in which condition this is sent, but it is hopefully triggered when the DPI switches are used.
			// There is nothing looking like a device ID here, though. I'm wondering how notifications for multiple devices work in that case.
			sink.OnDeviceDpiChange(streamIndex, BigEndian.ReadUInt16(span[1]), BigEndian.ReadUInt16(span[3]));
			break;
		case 0x09:
			// There are two parameters to this notification.
			// The second one is very likely a one-based device index, so we'll use it as such for now.
			switch (span[1])
			{
			case 2: sink.OnDeviceRemoval(streamIndex, span[2]); break;
			case 3: sink.OnDeviceArrival(streamIndex, span[2]); break;
			}
			break;
		case 0x0c:
			// This notification seems to indicate when the device is charging or not.
			// It is sent when connected to or disconnected from the dock, even when at 100%.
			sink.OnDeviceExternalPowerChange(streamIndex, (span[1] & 1) != 0);
			break;
		case 0x10:
			// Unknown notification that is often observed over Bluetooth.
			break;
		case 0x31:
			// This is a battery level notification with some extra data in it.
			sink.OnDeviceBatteryLevelChange(streamIndex, span[4], span[1]);
			break;
		case 0x35:
			// This is a device arrival/departure notification that seemingly is only present on the second stream and replaces notification `09`.
			switch (span[1])
			{
			case 2: sink.OnDeviceRemoval(streamIndex, span[2], BigEndian.ReadUInt16(in span[3])); break;
			case 3: sink.OnDeviceArrival(streamIndex, span[2], BigEndian.ReadUInt16(in span[3])); break;
			}
			break;
		default:
			break;
		}
	}
}
