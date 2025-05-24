using System.Collections.Immutable;
using Exo.Monitors;
using Exo.Primitives;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchMonitorDevicesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<MonitorInformation>.CreateAsync(_server.MonitorService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<MonitorInformation> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var monitorInformation in initialData)
					{
						int length = Write(buffer.Span, monitorInformation);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<MonitorInformation> watcher, CancellationToken cancellationToken)
		{
			while (await watcher.Reader.WaitToReadAsync().ConfigureAwait(false) && !cancellationToken.IsCancellationRequested)
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var monitorInformation))
					{
						int length = Write(buffer.Span, monitorInformation);
						if (cancellationToken.IsCancellationRequested) return;
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in MonitorInformation monitor)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.MonitorDevice);
			writer.Write(monitor.DeviceId);
			WriteSettings(ref writer, monitor.SupportedSettings);
			WriteValueDescriptions(ref writer, monitor.InputSelectSources);
			WriteValueDescriptions(ref writer, monitor.InputLagLevels);
			WriteValueDescriptions(ref writer, monitor.ResponseTimeLevels);
			WriteValueDescriptions(ref writer, monitor.OsdLanguages);
			return (int)writer.Length;
		}

		static void WriteSettings(ref BufferWriter writer, ImmutableArray<MonitorSetting> settings)
		{
			if (settings.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
				return;
			}
			writer.WriteVariable((uint)settings.Length);
			foreach (var setting in settings)
			{
				writer.WriteVariable((uint)setting);
			}
		}

		static void WriteValueDescriptions(ref BufferWriter writer, ImmutableArray<NonContinuousValueDescription> descriptions)
		{
			if (descriptions.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
				return;
			}
			writer.WriteVariable((uint)descriptions.Length);
			foreach (var description in descriptions)
			{
				WriteValueDescription(ref writer, description);
			}
		}

		static void WriteValueDescription(ref BufferWriter writer, NonContinuousValueDescription description)
		{
			writer.Write(description.Value);
			writer.Write(description.NameStringId);
			writer.WriteVariableString(description.CustomName);
		}
	}

	private async Task WatchMonitorSettingsAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<MonitorSettingValue>.CreateAsync(_server.MonitorService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<MonitorSettingValue> watcher, CancellationToken cancellationToken)
		{
			var initialData = watcher.ConsumeInitialData();
			if (initialData is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					foreach (var setting in initialData)
					{
						int length = Write(buffer.Span, setting);
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<MonitorSettingValue> watcher, CancellationToken cancellationToken)
		{
			while (await watcher.Reader.WaitToReadAsync().ConfigureAwait(false) && !cancellationToken.IsCancellationRequested)
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var setting))
					{
						int length = Write(buffer.Span, setting);
						if (cancellationToken.IsCancellationRequested) return;
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int Write(Span<byte> buffer, in MonitorSettingValue notification)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.MonitorSetting);
			writer.Write(notification.DeviceId);
			writer.WriteVariable((uint)notification.Setting);
			writer.Write(notification.CurrentValue);
			writer.Write(notification.MinimumValue);
			writer.Write(notification.MaximumValue);
			return (int)writer.Length;
		}
	}

	private void ProcessMonitorSettingSet(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		uint requestId = reader.ReadVariableUInt32();
		var deviceId = reader.ReadGuid();
		var setting = (MonitorSetting)reader.ReadVariableUInt32();
		var value = reader.Read<ushort>();
		ProcessMonitorSettingSet(requestId, deviceId, setting, value, cancellationToken);
	}

	private async void ProcessMonitorSettingSet(uint requestId, Guid deviceId, MonitorSetting setting, ushort value, CancellationToken cancellationToken)
	{
		var status = MonitorOperationStatus.Success;
		try
		{
			await _server.MonitorService.SetSettingValueAsync(deviceId, setting, value, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (DeviceNotFoundException)
		{
			status = MonitorOperationStatus.DeviceNotFound;
		}
		catch (SettingNotFoundException)
		{
			status = MonitorOperationStatus.SettingNotFound;
		}
		catch (Exception)
		{
			status = MonitorOperationStatus.Error;
		}
		try
		{
			await WriteMonitorOperationStatusAsync(ExoUiProtocolServerMessage.MonitorSettingSetStatus, requestId, status, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
			// This should never happen under normal conditions, but we don't want to crash.
		}
	}

	private void ProcessMonitorSettingRefresh(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		uint requestId = reader.ReadVariableUInt32();
		var deviceId = reader.ReadGuid();
		ProcessMonitorSettingRefresh(requestId, deviceId, cancellationToken);
	}

	private async void ProcessMonitorSettingRefresh(uint requestId, Guid deviceId, CancellationToken cancellationToken)
	{
		var status = MonitorOperationStatus.Success;
		try
		{
			await _server.MonitorService.RefreshValuesAsync(deviceId, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (DeviceNotFoundException)
		{
			status = MonitorOperationStatus.DeviceNotFound;
		}
		catch (Exception)
		{
			status = MonitorOperationStatus.Error;
		}
		try
		{
			await WriteMonitorOperationStatusAsync(ExoUiProtocolServerMessage.MonitorSettingSetStatus, requestId, status, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
			// This should never happen under normal conditions, but we don't want to crash.
		}
	}

	private async ValueTask WriteMonitorOperationStatusAsync(ExoUiProtocolServerMessage message, uint requestId, MonitorOperationStatus status, CancellationToken cancellationToken)
	{
		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = WriteBuffer;
			nuint length = Write(buffer.Span, message, requestId, status);
			await WriteAsync(buffer[..(int)length], cancellationToken).ConfigureAwait(false);
		}
		static nuint Write(Span<byte> buffer, ExoUiProtocolServerMessage message, uint requestId, MonitorOperationStatus status)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)message);
			writer.WriteVariable(requestId);
			writer.Write((byte)status);
			return writer.Length;
		}
	}
}
