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
		bool hasBluetoothHidQuirk = deviceNotificationOptions.HasBluetoothHidQuirk;
		byte reportId = deviceNotificationOptions.ReportId;
		_readTask = ReadAsync(stream, sink, buffer, hasBluetoothHidQuirk, reportId, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _readTask.ConfigureAwait(false);
		await _stream.DisposeAsync().ConfigureAwait(false);
	}

	private static async Task ReadAsync(DeviceStream stream, IRazerDeviceNotificationSink sink, Memory<byte> buffer, bool hasBluetoothHidQuirk, byte reportId, CancellationToken cancellationToken)
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

			HandleNotification(sink, buffer.Span, hasBluetoothHidQuirk, reportId);
		}
	}

	private static void HandleNotification(IRazerDeviceNotificationSink sink, Span<byte> span, bool hasBluetoothHidQuirk, byte reportId)
	{
		// For now, silently ignore potentially invalid data.
		// We might want to log this in the future so that bugs can be detected more easily.
		if (span[0] == reportId)
		{
			if (!hasBluetoothHidQuirk)
			{
				HandleNotification(sink, span[1..]);
			}
			else if (span[1] == reportId)
			{
				HandleNotification(sink, span[2..]);
			}
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
		case 16:
			// Unknown notification that is often observed over Bluetooth.
		default:
			break;
		}
	}
}
