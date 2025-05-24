using System.Collections.Immutable;
using System.Numerics;
using Exo.Cooling;
using Exo.Cooling.Configuration;
using Exo.Primitives;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchCoolingDevicesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<CoolingDeviceInformation>.CreateAsync(_server.CoolingService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<CoolingDeviceInformation> watcher, CancellationToken cancellationToken)
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

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<CoolingDeviceInformation> watcher, CancellationToken cancellationToken)
		{
			while (await watcher.Reader.WaitToReadAsync().ConfigureAwait(false) && !cancellationToken.IsCancellationRequested)
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var deviceInformation))
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						if (cancellationToken.IsCancellationRequested) return;
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteNotification(Span<byte> buffer, in CoolingDeviceInformation device)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.CoolingDevice);
			Write(ref writer, device);
			return (int)writer.Length;
		}
	}

	private async Task WatchCoolingConfigurationChangesAsync(CancellationToken cancellationToken)
	{
		using (var watcher = await BroadcastedChangeWatcher<CoolingUpdate>.CreateAsync(_server.CoolingService, cancellationToken))
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

		async Task WriteInitialDataAsync(BroadcastedChangeWatcher<CoolingUpdate> watcher, CancellationToken cancellationToken)
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

		async Task WriteConsumedDataAsync(BroadcastedChangeWatcher<CoolingUpdate> watcher, CancellationToken cancellationToken)
		{
			while (await watcher.Reader.WaitToReadAsync().ConfigureAwait(false) && !cancellationToken.IsCancellationRequested)
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					while (watcher.Reader.TryRead(out var deviceInformation))
					{
						int length = WriteNotification(buffer.Span, deviceInformation);
						if (cancellationToken.IsCancellationRequested) return;
						await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		static int WriteNotification(Span<byte> buffer, in CoolingUpdate update)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.CoolerConfiguration);
			Write(ref writer, update);
			return (int)writer.Length;
		}
	}

	private void ProcessCoolerSetAutomatic(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessCoolerSetAutomatic(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), cancellationToken);
	}

	private async void ProcessCoolerSetAutomatic(uint requestId, Guid deviceId, Guid coolerId, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _server.CoolingService.SetAutomaticPowerAsync(deviceId, coolerId, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.CoolerNotFound, cancellationToken);
				return;
			}
			catch (ArgumentException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.InvalidArgument, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessCoolerSetFixed(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessCoolerSetFixed(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), reader.ReadByte(), cancellationToken);
	}

	private async void ProcessCoolerSetFixed(uint requestId, Guid deviceId, Guid coolerId, byte power, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await _server.CoolingService.SetFixedPowerAsync(deviceId, coolerId, power, cancellationToken).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.CoolerNotFound, cancellationToken);
				return;
			}
			catch (ArgumentException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.InvalidArgument, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessCoolerSetSoftwareCurve(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessCoolerSetSoftwareCurve
		(
			reader.ReadVariableUInt32(),
			reader.ReadGuid(),
			reader.ReadGuid(),
			reader.ReadGuid(),
			reader.ReadGuid(),
			reader.ReadByte(),
			Serializer.ReadControlCurve(ref reader),
			cancellationToken
		);
	}

	private void ProcessCoolerSetSoftwareCurve(uint requestId, Guid coolingDeviceId, Guid coolerId, Guid sensorDeviceId, Guid sensorId, byte defaultValue, CoolingControlCurveConfiguration controlCurve, CancellationToken cancellationToken)
	{
		switch (controlCurve)
		{
		case CoolingControlCurveConfiguration<byte> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<ushort> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<uint> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<ulong> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<UInt128> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<sbyte> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<short> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<int> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<long> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<Int128> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<Half> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<float> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<double> cc:
			ProcessCoolerSetSoftwareCurve(requestId, coolingDeviceId, coolerId, sensorDeviceId, sensorId, defaultValue, cc, cancellationToken);
			break;
		default:
			throw new NotImplementedException();
		}
	}

	private async void ProcessCoolerSetSoftwareCurve<T>(uint requestId, Guid coolingDeviceId, Guid coolerId, Guid sensorDeviceId, Guid sensorId, byte defaultValue, CoolingControlCurveConfiguration<T> controlCurve, CancellationToken cancellationToken)
		where T : unmanaged, INumber<T>
	{
		try
		{
			try
			{
				await _server.CoolingService.SetSoftwareControlCurveAsync
				(
					coolingDeviceId,
					coolerId,
					sensorDeviceId,
					sensorId,
					defaultValue,
					new InterpolatedSegmentControlCurve<T, byte>(controlCurve.Points, MonotonicityValidators<byte>.IncreasingUpTo100),
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.CoolerNotFound, cancellationToken);
				return;
			}
			catch (ArgumentException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.InvalidArgument, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private void ProcessCoolerSetHardwareCurve(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		ProcessCoolerSetHardwareCurve(reader.ReadVariableUInt32(), reader.ReadGuid(), reader.ReadGuid(), reader.ReadGuid(), Serializer.ReadControlCurve(ref reader), cancellationToken);
	}

	private void ProcessCoolerSetHardwareCurve(uint requestId, Guid coolingDeviceId, Guid coolerId, Guid sensorId, CoolingControlCurveConfiguration controlCurve, CancellationToken cancellationToken)
	{
		switch (controlCurve)
		{
		case CoolingControlCurveConfiguration<byte> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<ushort> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<uint> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<ulong> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<UInt128> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<sbyte> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<short> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<int> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<long> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<Int128> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<Half> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<float> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		case CoolingControlCurveConfiguration<double> cc:
			ProcessCoolerSetHardwareCurve(requestId, coolingDeviceId, coolerId, sensorId, cc, cancellationToken);
			break;
		default:
			throw new NotImplementedException();
		}
	}

	private async void ProcessCoolerSetHardwareCurve<T>(uint requestId, Guid coolingDeviceId, Guid coolerId, Guid sensorId, CoolingControlCurveConfiguration<T> controlCurve, CancellationToken cancellationToken)
		where T : unmanaged, INumber<T>
	{
		try
		{
			try
			{
				await _server.CoolingService.SetHardwareControlCurveAsync
				(
					coolingDeviceId,
					coolerId,
					sensorId,
					new InterpolatedSegmentControlCurve<T, byte>(controlCurve.Points, MonotonicityValidators<byte>.IncreasingUpTo100),
					cancellationToken
				).ConfigureAwait(false);
			}
			catch (DeviceNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.DeviceNotFound, cancellationToken);
				return;
			}
			catch (MonitorNotFoundException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.CoolerNotFound, cancellationToken);
				return;
			}
			catch (ArgumentException)
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.InvalidArgument, cancellationToken);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch
			{
				await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Error, cancellationToken);
				return;
			}
			await WriteCoolingConfigurationStatusAsync(requestId, CoolingOperationStatus.Success, cancellationToken);
		}
		catch
		{
		}
	}

	private ValueTask WriteCoolingConfigurationStatusAsync(uint requestId, CoolingOperationStatus status, CancellationToken cancellationToken)
		=> WriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage.CoolingDeviceOperationStatus, requestId, (byte)status, cancellationToken);

	private static void Write(ref BufferWriter writer, in CoolingDeviceInformation device)
	{
		writer.Write(device.DeviceId);
		Write(ref writer, device.Coolers);
	}

	private static void Write(ref BufferWriter writer, in CoolingUpdate device)
	{
		writer.Write(device.DeviceId);
		writer.Write(device.CoolerId);
		Write(ref writer, device.CoolingMode);
	}

	private static void Write(ref BufferWriter writer, CoolingModeConfiguration coolingMode)
	{
		switch (coolingMode)
		{
		case AutomaticCoolingModeConfiguration:
			writer.Write((byte)ConfiguredCoolingMode.Automatic);
			break;
		case FixedCoolingModeConfiguration fixedCoolingMode:
			writer.Write((byte)ConfiguredCoolingMode.Fixed);
			Write(ref writer, fixedCoolingMode);
			break;
		case SoftwareCurveCoolingModeConfiguration softwareCurveCoolingMode:
			writer.Write((byte)ConfiguredCoolingMode.SoftwareCurve);
			Write(ref writer, softwareCurveCoolingMode);
			break;
		case HardwareCurveCoolingModeConfiguration hardwareCurveCoolingMode:
			writer.Write((byte)ConfiguredCoolingMode.HardwareCurve);
			Write(ref writer, hardwareCurveCoolingMode);
			break;
		}
	}

	private static void Write(ref BufferWriter writer, FixedCoolingModeConfiguration coolingMode)
	{
		writer.Write(coolingMode.Power);
	}

	private static void Write(ref BufferWriter writer, SoftwareCurveCoolingModeConfiguration coolingMode)
	{
		writer.Write(coolingMode.SensorDeviceId);
		writer.Write(coolingMode.SensorId);
		writer.Write(coolingMode.DefaultPower);
		Serializer.Write(ref writer, coolingMode.Curve);
	}

	private static void Write(ref BufferWriter writer, HardwareCurveCoolingModeConfiguration coolingMode)
	{
		writer.Write(coolingMode.SensorId);
		Serializer.Write(ref writer, coolingMode.Curve);
	}

	private static void Write(ref BufferWriter writer, ImmutableArray<CoolerInformation> coolers)
	{
		if (coolers.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)coolers.Length);
			foreach (var cooler in coolers)
			{
				Write(ref writer, cooler);
			}
		}
	}

	private static void Write(ref BufferWriter writer, CoolerInformation cooler)
	{
		writer.Write(cooler.CoolerId);
		writer.Write(cooler.SpeedSensorId.GetValueOrDefault());
		writer.Write((byte)cooler.Type);
		writer.Write((byte)cooler.SupportedCoolingModes);
		if (cooler.PowerLimits is { } powerLimits)
		{
			writer.Write(true);
			writer.Write(powerLimits.MinimumPower);
			writer.Write(powerLimits.CanSwitchOff);
		}
		else
		{
			writer.Write(false);
		}
		Write(ref writer, cooler.HardwareCurveInputSensorIds);
	}

	private static void Write(ref BufferWriter writer, ImmutableArray<Guid> guids)
	{
		if (guids.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)guids.Length);
			foreach (var guid in guids)
			{
				writer.Write(guid);
			}
		}
	}
}
