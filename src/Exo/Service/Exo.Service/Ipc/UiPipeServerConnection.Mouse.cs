using System.Collections.Immutable;
using Exo.Primitives;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchMouseDevicesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<MouseDeviceInformation>.CreateAsync(_mouseService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<MouseDeviceInformation> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var deviceInformation in initialData)
					{
						int length = WriteDevice(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<MouseDeviceInformation> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var deviceInformation))
					{
						int length = WriteDevice(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteDevice(Span<byte> buffer, in MouseDeviceInformation device)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.MouseDevice);
			Write(ref writer, in device);
			return (int)writer.Length;
		}
	}

	private async Task WatchMouseDpiAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<MouseDpiNotification>.CreateAsync(_mouseService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<MouseDpiNotification> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var deviceInformation in initialData)
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<MouseDpiNotification> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var deviceInformation))
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteNotification(Span<byte> buffer, in MouseDpiNotification notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.MouseDpi);
			Write(ref writer, in notification);
			return (int)writer.Length;
		}
	}

	private async Task WatchMouseDpiPresetsAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<MouseDpiPresetsInformation>.CreateAsync(_mouseService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<MouseDpiPresetsInformation> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var deviceInformation in initialData)
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<MouseDpiPresetsInformation> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var deviceInformation))
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteNotification(Span<byte> buffer, in MouseDpiPresetsInformation notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.MouseDpiPresets);
			Write(ref writer, in notification);
			return (int)writer.Length;
		}
	}

	private async Task WatchMousePollingFrequencyAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<MousePollingFrequencyNotification>.CreateAsync(_mouseService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<MousePollingFrequencyNotification> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var deviceInformation in initialData)
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<MousePollingFrequencyNotification> watcher, CancellationToken cancellationToken)
		{
			while (true)
			{
				await watcher.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var deviceInformation))
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteNotification(Span<byte> buffer, in MousePollingFrequencyNotification notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.MousePollingFrequency);
			Write(ref writer, in notification);
			return (int)writer.Length;
		}
	}

	private void ProcessMouseActiveDpiPreset(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessMouseActiveDpiPreset(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadByte(), cancellationToken);
	}

	private async void ProcessMouseActiveDpiPreset(uint requestId, Guid deviceId, byte activePresetIndex, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _mouseService.SetActiveDpiPresetAsync(deviceId, activePresetIndex, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessMouseDpiPresets(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessMouseDpiPresets(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadByte(), Serializer.ReadDotsPerInches(ref reader), cancellationToken);
	}

	private async void ProcessMouseDpiPresets(uint requestId, Guid deviceId, byte activePresetIndex, ImmutableArray<DotsPerInch> presets, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _mouseService.SetDpiPresetsAsync(deviceId, activePresetIndex, presets, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessMousePollingFrequency(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessMousePollingFrequency(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.Read<ushort>(), cancellationToken);
	}

	private async void ProcessMousePollingFrequency(uint requestId, Guid deviceId, ushort pollingFrequency, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _mouseService.SetPollingFrequencyAsync(deviceId, pollingFrequency, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteMouseDeviceConfigurationStatusAsync(requestId, MouseDeviceOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private ValueTask WriteMouseDeviceConfigurationStatusAsync(uint requestId, MouseDeviceOperationStatus status, CancellationToken cancellationToken)
		=> WriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage.MouseDeviceOperationStatus, requestId, (byte)status, cancellationToken);

	private static void Write(ref BufferWriter writer, in MouseDeviceInformation device)
	{
		writer.Write(device.DeviceId);
		writer.Write(device.IsConnected);
		writer.Write((byte)device.Capabilities);
		Serializer.Write(ref writer, device.MaximumDpi);
		if ((device.Capabilities & (MouseCapabilities.DpiPresets | MouseCapabilities.ConfigurableDpiPresets)) != 0)
		{
			writer.Write(device.MinimumDpiPresetCount);
			writer.Write(device.MaximumDpiPresetCount);
		}
		if (device.SupportedPollingFrequencies.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)device.SupportedPollingFrequencies.Length);
			foreach (var frequency in device.SupportedPollingFrequencies)
			{
				writer.Write(frequency);
			}
		}
	}

	private static void Write(ref BufferWriter writer, in MouseDpiNotification notification)
	{
		writer.Write(notification.DeviceId);
		var value = notification.NewValue;
		if (value.PresetIndex is not null)
		{
			writer.Write((byte)1);
			writer.Write(value.PresetIndex.GetValueOrDefault());
		}
		else
		{
			writer.Write((byte)0);
		}
		Serializer.Write(ref writer, value.Dpi);
	}

	private static void Write(ref BufferWriter writer, in MouseDpiPresetsInformation dpiPresets)
	{
		writer.Write(dpiPresets.DeviceId);
		if (dpiPresets.ActivePresetIndex is not null)
		{
			writer.Write((byte)1);
			writer.Write(dpiPresets.ActivePresetIndex.GetValueOrDefault());
		}
		else
		{
			writer.Write((byte)0);
		}
		Serializer.Write(ref writer, dpiPresets.DpiPresets);
	}

	private static void Write(ref BufferWriter writer, in MousePollingFrequencyNotification notification)
	{
		writer.Write(notification.DeviceId);
		writer.Write(notification.PollingFrequency);
	}
}
