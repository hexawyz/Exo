using System.Collections.Immutable;
using Exo.Primitives;
using Exo.Settings.Ui.Ipc;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchLightDevicesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<LightDeviceInformation>.CreateAsync(_lightService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<LightDeviceInformation> watcher, CancellationToken cancellationToken)
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

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<LightDeviceInformation> watcher, CancellationToken cancellationToken)
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

		static int WriteNotification(Span<byte> buffer, in LightDeviceInformation notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.LightDevice);
			Write(ref writer, notification);
			return (int)writer.Length;
		}
	}

	private async Task WatchLightConfigurationChangesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<LightChangeNotification>.CreateAsync(_lightService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<LightChangeNotification> watcher, CancellationToken cancellationToken)
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

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<LightChangeNotification> watcher, CancellationToken cancellationToken)
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

		static int WriteNotification(Span<byte> buffer, in LightChangeNotification notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.LightConfiguration);
			Write(ref writer, notification);
			return (int)writer.Length;
		}
	}

	private void ProcessLightSwitch(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessLightSwitch(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), reader.ReadBoolean(), cancellationToken);
	}

	private async void ProcessLightSwitch(uint requestId, Guid deviceId, Guid lightId, bool isOn, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _lightService.SwitchLightAsync(deviceId, lightId, isOn, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.LightNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessLightBrightness(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessLightBrightness(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), reader.ReadByte(), cancellationToken);
	}

	private async void ProcessLightBrightness(uint requestId, Guid deviceId, Guid lightId, byte brightness, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _lightService.SetBrightnessAsync(deviceId, lightId, brightness, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.LightNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessLightTemperature(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessLightTemperature(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), reader.Read<uint>(), cancellationToken);
	}

	private async void ProcessLightTemperature(uint requestId, Guid deviceId, Guid lightId, uint temperature, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _lightService.SetTemperatureAsync(deviceId, lightId, temperature, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.LightNotFound, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteLightConfigurationStatusAsync(requestId, LightOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private ValueTask WriteLightConfigurationStatusAsync(uint requestId, LightOperationStatus status, CancellationToken cancellationToken)
		=> WriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage.PowerDeviceOperationStatus, requestId, (byte)status, cancellationToken);

	private static void Write(ref BufferWriter writer, in LightDeviceInformation deviceInformation)
	{
		writer.Write(deviceInformation.DeviceId);
		writer.Write((byte)deviceInformation.Capabilities);
		Write(ref writer, deviceInformation.Lights);
	}

	private static void Write(ref BufferWriter writer, ImmutableArray<LightInformation> lightsInformations)
	{
		if (lightsInformations.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)lightsInformations.Length);
			foreach (var lightsInformation in lightsInformations)
			{
				Write(ref writer, lightsInformation);
			}
		}
	}

	private static void Write(ref BufferWriter writer, in LightChangeNotification notification)
	{
		writer.Write(notification.DeviceId);
		writer.Write(notification.LightId);
		writer.Write(notification.IsOn);
		writer.Write(notification.Brightness);
		writer.Write(notification.Temperature);
	}

	private static void Write(ref BufferWriter writer, in LightInformation lightsInformation)
	{
		writer.Write(lightsInformation.LightId);
		writer.Write((byte)lightsInformation.Capabilities);
		writer.Write(lightsInformation.MinimumBrightness);
		writer.Write(lightsInformation.MaximumBrightness);
		writer.Write(lightsInformation.MinimumTemperature);
		writer.Write(lightsInformation.MaximumTemperature);
	}
}
