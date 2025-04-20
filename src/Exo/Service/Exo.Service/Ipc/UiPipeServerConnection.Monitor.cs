using System.Collections.Immutable;
using Exo.Monitors;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchMonitorDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var info in _monitorService.WatchMonitorsAsync(cancellationToken).ConfigureAwait(false))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int length = WriteUpdate(buffer.Span, info);
					await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}

		static int WriteUpdate(Span<byte> buffer, in MonitorInformation monitor)
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
		try
		{
			await foreach (var setting in _monitorService.WatchSettingsAsync(cancellationToken).ConfigureAwait(false))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int length = Write(buffer.Span, setting);
					await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
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
			await _monitorService.SetSettingValueAsync(deviceId, setting, value, cancellationToken).ConfigureAwait(false);
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
			await _monitorService.RefreshValuesAsync(deviceId, cancellationToken).ConfigureAwait(false);
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
