using System.Collections.Immutable;
using Exo.EmbeddedMonitors;
using Exo.Images;
using Exo.Primitives;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchEmbeddedMonitorDevicesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<EmbeddedMonitorDeviceInformation>.CreateAsync(_server.EmbeddedMonitorService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<EmbeddedMonitorDeviceInformation> watcher, CancellationToken cancellationToken)
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

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<EmbeddedMonitorDeviceInformation> watcher, CancellationToken cancellationToken)
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

		static int WriteNotification(Span<byte> buffer, in EmbeddedMonitorDeviceInformation notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.EmbeddedMonitorDevice);
			Write(ref writer, notification);
			return (int)writer.Length;
		}
	}

	private async Task WatchEmbeddedMonitorConfigurationChangesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<EmbeddedMonitorConfiguration>.CreateAsync(_server.EmbeddedMonitorService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<EmbeddedMonitorConfiguration> watcher, CancellationToken cancellationToken)
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

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<EmbeddedMonitorConfiguration> watcher, CancellationToken cancellationToken)
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

		static int WriteNotification(Span<byte> buffer, in EmbeddedMonitorConfiguration notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.EmbeddedMonitorConfiguration);
			Write(ref writer, notification);
			return (int)writer.Length;
		}
	}

	private void ProcessEmbeddedMonitorBuiltInGraphics(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessEmbeddedMonitorBuiltInGraphics(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), reader.ReadGuid(), cancellationToken);
	}

	private async void ProcessEmbeddedMonitorBuiltInGraphics(uint requestId, Guid deviceId, Guid monitorId, Guid graphicsId, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _server.EmbeddedMonitorService.SetBuiltInGraphicsAsync(deviceId, monitorId, graphicsId, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.MonitorNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessEmbeddedMonitorImage(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessEmbeddedMonitorImage(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), reader.Read<UInt128>(), Serializer.ReadRectangle(ref reader), cancellationToken);
	}

	private async void ProcessEmbeddedMonitorImage(uint requestId, Guid deviceId, Guid monitorId, UInt128 imageId, Rectangle imageRegion, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _server.EmbeddedMonitorService.SetImageAsync(deviceId, monitorId, imageId, imageRegion, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.MonitorNotFound, cancellationToken);
				return;
			}
			catch (ArgumentException)
			{
				await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.InvalidArgument, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteEmbeddedMonitorConfigurationStatusAsync(requestId, EmbeddedMonitorOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private ValueTask WriteEmbeddedMonitorConfigurationStatusAsync(uint requestId, EmbeddedMonitorOperationStatus status, CancellationToken cancellationToken)
		=> WriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage.EmbeddedMonitorDeviceOperationStatus, requestId, (byte)status, cancellationToken);

	private static void Write(ref BufferWriter writer, in EmbeddedMonitorDeviceInformation deviceInformation)
	{
		writer.Write(deviceInformation.DeviceId);
		Write(ref writer, deviceInformation.EmbeddedMonitors);
	}

	private static void Write(ref BufferWriter writer, ImmutableArray<EmbeddedMonitorInformation> embeddedMonitorInformations)
	{
		if (embeddedMonitorInformations.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)embeddedMonitorInformations.Length);
			foreach (var embeddedMonitorInformation in embeddedMonitorInformations)
			{
				Write(ref writer, embeddedMonitorInformation);
			}
		}
	}

	private static void Write(ref BufferWriter writer, in EmbeddedMonitorInformation embeddedMonitorInformation)
	{
		writer.Write(embeddedMonitorInformation.MonitorId);
		writer.Write((byte)embeddedMonitorInformation.Shape);
		writer.Write((byte)embeddedMonitorInformation.DefaultRotation);
		Serializer.Write(ref writer, embeddedMonitorInformation.ImageSize);
		writer.Write(embeddedMonitorInformation.PixelFormat);
		writer.Write((uint)embeddedMonitorInformation.SupportedImageFormats);
		writer.Write((byte)embeddedMonitorInformation.Capabilities);
		Write(ref writer, embeddedMonitorInformation.SupportedGraphics);
	}

	private static void Write(ref BufferWriter writer, ImmutableArray<EmbeddedMonitorGraphicsDescription> descriptions)
	{
		if (descriptions.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)descriptions.Length);
			foreach (var description in descriptions)
			{
				Write(ref writer, description);
			}
		}
	}

	private static void Write(ref BufferWriter writer, in EmbeddedMonitorGraphicsDescription description)
	{
		writer.Write(description.GraphicsId);
		writer.Write(description.NameStringId);
	}

	private static void Write(ref BufferWriter writer, in EmbeddedMonitorConfiguration notification)
	{
		writer.Write(notification.DeviceId);
		writer.Write(notification.MonitorId);
		writer.Write(notification.GraphicsId);
		writer.Write(notification.ImageId);
		Serializer.Write(ref writer, notification.ImageRegion);
	}
}
