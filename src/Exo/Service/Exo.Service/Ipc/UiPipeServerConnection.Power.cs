using Exo.Features;
using Exo.Primitives;

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
					foreach (var deviceInformation in initialData)
					{
						int length = Write(buffer.Span, deviceInformation);
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
					while (watcher.Reader.TryRead(out var deviceInformation))
					{
						int length = Write(buffer.Span, deviceInformation);
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

	private async Task WatchBatteryStateChangesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<ChangeWatchNotification<Guid, BatteryState>>.CreateAsync(_powerService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<ChangeWatchNotification<Guid, BatteryState>> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var notification in initialData)
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<ChangeWatchNotification<Guid, BatteryState>> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var notification))
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in ChangeWatchNotification<Guid, BatteryState> notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.BatteryState);
			writer.Write(notification.Key);
			WriteBatteryState(ref writer, notification.NewValue);

			return (int)writer.Length;
		}

		static void WriteBatteryState(ref BufferWriter writer, BatteryState state)
		{
			if (state.Level is not null)
			{
				writer.Write((byte)1);
				writer.Write(state.Level.GetValueOrDefault());
			}
			else
			{
				writer.Write((byte)1);
			}
			writer.Write((byte)state.BatteryStatus);
			writer.Write((byte)state.ExternalPowerStatus);
		}
	}

	private async Task WatchLowPowerBatteryThresholdUpdatesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<PowerDeviceLowPowerBatteryThresholdNotification>.CreateAsync(_powerService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<PowerDeviceLowPowerBatteryThresholdNotification> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var notification in initialData)
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<PowerDeviceLowPowerBatteryThresholdNotification> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var notification))
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in PowerDeviceLowPowerBatteryThresholdNotification notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.LowPowerBatteryThreshold);
			writer.Write(notification.DeviceId);
			writer.Write(notification.BatteryThreshold);

			return (int)writer.Length;
		}
	}

	private async Task WatchIdleSleepTimerUpdatesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<PowerDeviceIdleSleepTimerNotification>.CreateAsync(_powerService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<PowerDeviceIdleSleepTimerNotification> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var notification in initialData)
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<PowerDeviceIdleSleepTimerNotification> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var notification))
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in PowerDeviceIdleSleepTimerNotification notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.IdleSleepTimer);
			writer.Write(notification.DeviceId);
			writer.Write((ulong)notification.IdleTime.Ticks);

			return (int)writer.Length;
		}
	}

	private async Task WatchWirelessBrightnessUpdatesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<PowerDeviceWirelessBrightnessNotification>.CreateAsync(_powerService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<PowerDeviceWirelessBrightnessNotification> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var notification in initialData)
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<PowerDeviceWirelessBrightnessNotification> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var notification))
					{
						int length = Write(buffer.Span, notification);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in PowerDeviceWirelessBrightnessNotification notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.WirelessBrightness);
			writer.Write(notification.DeviceId);
			writer.Write(notification.Brightness);

			return (int)writer.Length;
		}
	}

	private void ProcessLowPowerBatteryThreshold(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessLowPowerBatteryThreshold(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.Read<Half>(), cancellationToken);
	}

	private async void ProcessLowPowerBatteryThreshold(uint requestId, Guid deviceId, Half threshold, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _powerService.SetLowPowerModeBatteryThresholdAsync(deviceId, threshold, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.Error, cancellationToken);
				return;
			}
			await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessIdleSleepTimer(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessIdleSleepTimer(reader.ReadVariableUInt32(), reader.ReadGuid(), TimeSpan.FromTicks((long)reader.Read<ulong>()), cancellationToken);
	}

	private async void ProcessIdleSleepTimer(uint requestId, Guid deviceId, TimeSpan idleTimer, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _powerService.SetIdleSleepTimerAsync(deviceId, idleTimer, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.Error, cancellationToken);
				return;
			}
			await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessWirelessBrightness(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessWirelessBrightness(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadByte(), cancellationToken);
	}

	private async void ProcessWirelessBrightness(uint requestId, Guid deviceId, byte brightness, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _powerService.SetWirelessBrightnessAsync(deviceId, brightness, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.Error, cancellationToken);
				return;
			}
			await WritePowerDeviceDeviceConfigurationStatusAsync(requestId, PowerDeviceOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private ValueTask WritePowerDeviceDeviceConfigurationStatusAsync(uint requestId, PowerDeviceOperationStatus status, CancellationToken cancellationToken)
		=> WriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage.PowerDeviceOperationStatus, requestId, (byte)status, cancellationToken);
}
