using Exo.Primitives;
using Exo.Settings.Ui.Ipc;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchPowerDevicesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<PowerDeviceInformation>.CreateAsync(_powerService, cancellationToken))
		{
			try
			{
				await WriteInitialDataAsync(watcher, cancellationToken).ConfigureAwait(false);
				await WriteConsumedDataAsync(watcher, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<PowerDeviceInformation> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var effectInformation in initialData)
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<PowerDeviceInformation> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var effectInformation))
					{
						int length = Write(buffer.Span, effectInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in PowerDeviceInformation device)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.PowerDevice);
			writer.Write(device.DeviceId);
			writer.Write((byte)device.Flags);
			if ((device.Flags & PowerDeviceFlags.HasIdleTimer) != 0)
			{
				writer.Write(device.MinimumIdleTime.Ticks);
				writer.Write(device.MaximumIdleTime.Ticks);
			}
			if ((device.Flags & PowerDeviceFlags.HasWirelessBrightness) != 0)
			{
				writer.Write(device.MinimumBrightness);
				writer.Write(device.MaximumBrightness);
			}

			return (int)writer.Length;
		}
	}
}
