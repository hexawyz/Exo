using System.Collections.Immutable;
using System.IO.Pipes;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.Contracts.Ui;
using Exo.Primitives;
using Exo.Ipc;
using Exo.Service;
using Exo.Settings.Ui.Services;
using Exo.Utils;
using Microsoft.UI.Dispatching;
using DeviceTools;
using Exo.Monitors;
using Exo.Contracts;
using Exo.Contracts.Ui.Settings;
using Exo.Lighting;
using Exo.ColorFormats;

namespace Exo.Settings.Ui.Ipc;

internal sealed class ExoUiPipeClientConnection : PipeClientConnection, IPipeClientConnection<ExoUiPipeClientConnection>, IServiceControl
{
	private static readonly ImmutableArray<byte> GitCommitId = GitCommitHelper.GetCommitId(typeof(ExoUiPipeClientConnection).Assembly);

	public static ExoUiPipeClientConnection Create(PipeClient<ExoUiPipeClientConnection> client, NamedPipeClientStream stream)
	{
		var uiPipeClient = (ExoUiPipeClient)client;
		return new(client, stream, uiPipeClient.DispatcherQueue, uiPipeClient.ServiceClient);
	}

	private readonly DispatcherQueue _dispatcherQueue;
	private readonly IServiceClient _serviceClient;
	private SensorWatchOperation?[]? _sensorWatchOperations;
	private TaskCompletionSource<MonitorOperationStatus>?[]? _monitorOperations;
	private TaskCompletionSource<LightingDeviceOperationStatus>?[]? _lightingDeviceOperations;
	private bool _isConnected;

	private ExoUiPipeClientConnection
	(
		PipeClient client,
		NamedPipeClientStream stream,
		DispatcherQueue dispatcherQueue,
		IServiceClient serviceClient
	) : base(client, stream)
	{
		_dispatcherQueue = dispatcherQueue;
		_serviceClient = serviceClient;
	}

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
				if (count == 0) return;
				// If the message processing does not indicate success, we can close the connection.
				if (!await ProcessMessageAsync(buffer.Span[..count]).ConfigureAwait(false)) return;
			}
		}
		finally
		{
			if (_isConnected)
			{
				_isConnected = false;
				_dispatcherQueue.TryEnqueue(() => _serviceClient.OnDisconnected());
			}
		}
	}

	protected override ValueTask OnDisposedAsync()
	{
		if (Interlocked.Exchange(ref _sensorWatchOperations, null) is { } sensorWatchOperations)
			for (int i = 0; i < sensorWatchOperations.Length; i++)
				if (Interlocked.Exchange(ref sensorWatchOperations[i], null) is { } sensorWatchOperation)
					sensorWatchOperation.OnConnectionClosed();
		if (Interlocked.Exchange(ref _monitorOperations, null) is { } monitorOperations)
			for (int i = 0; i < monitorOperations.Length; i++)
				if (Interlocked.Exchange(ref monitorOperations[i], null) is { } monitorOperation)
					monitorOperation.TrySetCanceled();
		return ValueTask.CompletedTask;
	}

	private ValueTask<bool> ProcessMessageAsync(ReadOnlySpan<byte> data) => ProcessMessageAsync((ExoUiProtocolServerMessage)data[0], data[1..]);

	private ValueTask<bool> ProcessMessageAsync(ExoUiProtocolServerMessage message, ReadOnlySpan<byte> data)
	{
		switch (message)
		{
		case ExoUiProtocolServerMessage.NoOp:
			goto Success;
		case ExoUiProtocolServerMessage.GitVersion:
			if (data.Length != 20) goto Failure;
#if DEBUG
			return ConfirmVersionAsync(data.ToImmutableArray(), true);
#else
			if (GitCommitId.IsDefaultOrEmpty) return ConfirmVersionAsync(data.ToImmutableArray(), true);
			else return ConfirmVersionAsync(data.ToImmutableArray(), data.SequenceEqual(ImmutableCollectionsMarshal.AsArray(GitCommitId)));
#endif
		case ExoUiProtocolServerMessage.MetadataSourcesEnumeration:
			ProcessMetadataSource(Service.WatchNotificationKind.Enumeration, data);
			goto Success;
		case ExoUiProtocolServerMessage.MetadataSourcesAdd:
			ProcessMetadataSource(Service.WatchNotificationKind.Addition, data);
			goto Success;
		case ExoUiProtocolServerMessage.MetadataSourcesRemove:
			ProcessMetadataSource(Service.WatchNotificationKind.Removal, data);
			goto Success;
		case ExoUiProtocolServerMessage.MetadataSourcesUpdate:
			ProcessMetadataSource(Service.WatchNotificationKind.Update, data);
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemEnumeration:
			ProcessCustomMenu(Contracts.Ui.WatchNotificationKind.Enumeration, data);
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemAdd:
			ProcessCustomMenu(Contracts.Ui.WatchNotificationKind.Addition, data);
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemRemove:
			ProcessCustomMenu(Contracts.Ui.WatchNotificationKind.Removal, data);
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemUpdate:
			ProcessCustomMenu(Contracts.Ui.WatchNotificationKind.Update, data);
			goto Success;
		case ExoUiProtocolServerMessage.LightingEffect:
			ProcessLightingEffect(data);
			goto Success;
		case ExoUiProtocolServerMessage.DeviceEnumeration:
			ProcessDevice(Service.WatchNotificationKind.Enumeration, data);
			goto Success;
		case ExoUiProtocolServerMessage.DeviceAdd:
			ProcessDevice(Service.WatchNotificationKind.Addition, data);
			goto Success;
		case ExoUiProtocolServerMessage.DeviceRemove:
			ProcessDevice(Service.WatchNotificationKind.Removal, data);
			goto Success;
		case ExoUiProtocolServerMessage.DeviceUpdate:
			ProcessDevice(Service.WatchNotificationKind.Update, data);
			goto Success;
		case ExoUiProtocolServerMessage.PowerDevice:
			ProcessPowerDevice(data);
			goto Success;
		case ExoUiProtocolServerMessage.LightingDevice:
			ProcessLightingDevice(data);
			goto Success;
		case ExoUiProtocolServerMessage.LightingDeviceConfiguration:
			ProcessLightingDeviceConfiguration(data);
			goto Success;
		case ExoUiProtocolServerMessage.LightingDeviceConfigurationStatus:
			ProcessLightingDeviceOperationStatus(data);
			goto Success;
		case ExoUiProtocolServerMessage.MonitorDevice:
			ProcessMonitorDevice(data);
			goto Success;
		case ExoUiProtocolServerMessage.MonitorSetting:
			ProcessMonitorSetting(data);
			goto Success;
		case ExoUiProtocolServerMessage.MonitorSettingSetStatus:
		case ExoUiProtocolServerMessage.MonitorSettingRefreshStatus:
			ProcessMonitorOperationStatus(data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorDevice:
			ProcessSensorDevice(data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorStart:
			ProcessSensorStart(data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorValue:
			ProcessSensorValue(data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorStop:
			return ProcessSensorStopAsync(data);
		case ExoUiProtocolServerMessage.SensorConfiguration:
			ProcessSensorConfiguration(data);
			goto Success;
		}
	Failure:;
		return new(false);
	Success:;
		return new(true);
	}

	private async ValueTask<bool> ConfirmVersionAsync(ImmutableArray<byte> version, bool isVersionMatch)
	{
		if (version.IsDefault || version.Length != 20) throw new ArgumentException();

		using var cts = CreateWriteCancellationTokenSource(default);
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..21];
			FillBuffer(buffer.Span, version);
			await WriteAsync(buffer, cts.Token).ConfigureAwait(false);
		}

		if (isVersionMatch) _dispatcherQueue.TryEnqueue(() => _serviceClient.OnConnected(this));
		else _dispatcherQueue.TryEnqueue(() => _serviceClient.OnConnected(null));

		_isConnected = true;

		return true;

		static void FillBuffer(Span<byte> buffer, ImmutableArray<byte> version)
		{
			buffer[0] = (byte)ExoUiProtocolClientMessage.GitVersion;
			ImmutableCollectionsMarshal.AsArray(version)!.CopyTo(buffer[1..]);
		}
	}

	private void ProcessMetadataSource(Service.WatchNotificationKind kind, ReadOnlySpan<byte> data)
	{
		MetadataSourceChangeNotification notification;
		if (kind == Service.WatchNotificationKind.Update)
		{
			notification = new(Service.WatchNotificationKind.Update, []);
			goto PropagateNotification;
		}
		var reader = new BufferReader(data);
		var sources = new MetadataSourceInformation[reader.ReadVariableUInt32()];
		for (int i = 0; i < sources.Length; i++)
			sources[i] = new((MetadataArchiveCategory)reader.ReadByte(), reader.ReadVariableString() ?? "");
		notification = new MetadataSourceChangeNotification(kind, ImmutableCollectionsMarshal.AsImmutableArray(sources));
	PropagateNotification:;
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnMetadataSourceNotification(notification));
	}

	private void ProcessCustomMenu(Contracts.Ui.WatchNotificationKind kind, ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		var notification = new MenuChangeNotification()
		{
			Kind = kind,
			ParentItemId = reader.Read<Guid>(),
			Position = reader.Read<uint>(),
			ItemId = reader.Read<Guid>(),
			ItemType = (MenuItemType)reader.ReadByte(),
			Text = reader.RemainingLength > 0 ? reader.ReadVariableString() ?? "" : null
		};
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnMenuUpdate(notification));
	}

	private void ProcessLightingEffect(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		var effectId = reader.ReadGuid();
		uint propertyCount = reader.ReadVariableUInt32();
		ConfigurablePropertyInformation[] properties;
		if (propertyCount == 0)
		{
			properties = [];
		}
		else
		{
			properties = new ConfigurablePropertyInformation[propertyCount];

			for (int i = 0; i < propertyCount; i++)
			{
				string name = reader.ReadVariableString() ?? "";
				string displayName = reader.ReadVariableString() ?? "";
				var dataType = (DataType)reader.ReadByte();
				var flags = (LightingEffectFlags)reader.ReadByte();
				object? defaultValue = (flags & LightingEffectFlags.DefaultValue) != 0 ? ReadValue(ref reader, dataType) : null;
				object? minimumValue = (flags & LightingEffectFlags.MinimumValue) != 0 ? ReadValue(ref reader, dataType) : null;
				object? maximumValue = (flags & LightingEffectFlags.MaximumValue) != 0 ? ReadValue(ref reader, dataType) : null;
				EnumerationValue[] enumerationValues;
				if ((flags & LightingEffectFlags.Enum) == 0)
				{
					enumerationValues = [];
				}
				else
				{
					enumerationValues = new EnumerationValue[reader.ReadVariableUInt32()];
					for (int j = 0; j < enumerationValues.Length; j++)
					{
						enumerationValues[j] = new()
						{
							Value = ReadConstantValue(ref reader, dataType),
							DisplayName = reader.ReadVariableString() ?? "",
							Description = reader.ReadVariableString() ?? "",
						};
					}
				}
				properties[i] = new()
				{
					Name = name,
					DisplayName = displayName,
					DataType = dataType,
					DefaultValue = defaultValue,
					MinimumValue = minimumValue,
					MaximumValue = maximumValue,
					EnumerationValues = ImmutableCollectionsMarshal.AsImmutableArray(enumerationValues),
					ArrayLength = (flags & LightingEffectFlags.Array) != 0 ? (int)reader.ReadVariableUInt32() : null,
				};
			}
		}
		var effect = new LightingEffectInformation()
		{
			EffectId = effectId,
			Properties = ImmutableCollectionsMarshal.AsImmutableArray(properties),
		};
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnLightingEffectUpdate(effect));

		static object ReadValue(ref BufferReader reader, DataType dataType)
			=> dataType switch
			{
				DataType.UInt8 or DataType.ColorGrayscale8 => reader.ReadByte(),
				DataType.Int8 => (sbyte)reader.ReadByte(),
				DataType.UInt16 => reader.Read<ushort>(),
				DataType.Int16 => reader.Read<short>(),
				DataType.UInt32 or DataType.ColorRgbw32 or DataType.ColorArgb32 => reader.Read<uint>(),
				DataType.Int32 => reader.Read<int>(),
				DataType.UInt64 => reader.Read<ulong>(),
				DataType.Int64 => reader.Read<long>(),
				DataType.Float16 => reader.Read<Half>(),
				DataType.Float32 => reader.Read<float>(),
				DataType.Float64 => reader.Read<double>(),
				DataType.Boolean => reader.ReadByte(),
				DataType.Guid => reader.ReadGuid(),
				_ => throw new InvalidOperationException($"Type not supported: {dataType}."),
			};

		static ulong ReadConstantValue(ref BufferReader reader, DataType dataType)
			=> dataType switch
			{
				DataType.UInt8 => reader.ReadByte(),
				DataType.Int8 => (ulong)(long)(sbyte)reader.ReadByte(),
				DataType.UInt16 => reader.Read<ushort>(),
				DataType.Int16 => (ulong)(long)reader.Read<short>(),
				DataType.UInt32 => reader.Read<uint>(),
				DataType.Int32 => (ulong)(long)reader.Read<int>(),
				DataType.UInt64 or DataType.Int64 => reader.Read<ulong>(),
				_ => throw new InvalidOperationException($"Type not supported: {dataType}."),
			};
	}

	private void ProcessDevice(Service.WatchNotificationKind kind, ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		var deviceId = reader.ReadGuid();
		string friendlyName = reader.ReadVariableString() ?? "";
		string userFriendlyName = reader.ReadVariableString() ?? "";
		var category = (DeviceCategory)reader.ReadByte();
		var featureCount = reader.ReadVariableUInt32();
		var featureIds = new HashSet<Guid>();
		for (int i = 0; i < featureCount; i++)
		{
			featureIds.Add(reader.ReadGuid());
		}
		var deviceIds = new DeviceId[reader.ReadVariableUInt32()];
		for (int i = 0; i < deviceIds.Length; i++)
		{
			deviceIds[i] = new((DeviceIdSource)reader.ReadByte(), (VendorIdSource)reader.ReadByte(), reader.Read<ushort>(), reader.Read<ushort>(), reader.Read<ushort>());
		}
		byte flags = reader.ReadByte();
		int? mainDeviceIdIndex = (flags & 0x2) != 0 ? (int)reader.ReadVariableUInt32() : null;
		string? serialNumber = reader.ReadVariableString();
		var information = new DeviceStateInformation(deviceId, friendlyName, userFriendlyName, category, featureIds, ImmutableCollectionsMarshal.AsImmutableArray(deviceIds), mainDeviceIdIndex, serialNumber, (flags & 0x1) != 0);
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnDeviceNotification(kind, information));
	}

	private void ProcessPowerDevice(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		var deviceId = reader.ReadGuid();
		var flags = (PowerDeviceFlags)reader.ReadByte();
		TimeSpan minimumIdleTime;
		TimeSpan maximumIdleTime;
		if ((flags & PowerDeviceFlags.HasIdleTimer) != 0)
		{
			minimumIdleTime = TimeSpan.FromTicks((long)reader.Read<ulong>());
			maximumIdleTime = TimeSpan.FromTicks((long)reader.Read<ulong>());
		}
		else
		{
			minimumIdleTime = default;
			maximumIdleTime = default;
		}
		byte minimumBrightness;
		byte maximumBrightness;
		if ((flags & PowerDeviceFlags.HasWirelessBrightness) != 0)
		{
			minimumBrightness = reader.ReadByte();
			maximumBrightness = reader.ReadByte();
		}
		else
		{
			minimumBrightness = 0;
			maximumBrightness = 0;
		}
		var information = new PowerDeviceInformation(deviceId, flags, minimumIdleTime, maximumIdleTime, minimumBrightness, maximumBrightness);
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnPowerDeviceUpdate(information));
	}

	private void ProcessLightingDevice(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		var deviceId = reader.ReadGuid();
		var flags = (LightingDeviceFlags)reader.ReadByte();
		var persistenceMode = LightingPersistenceMode.NeverPersisted;
		if ((flags & LightingDeviceFlags.CanPersist) != 0)
		{
			persistenceMode = (flags & LightingDeviceFlags.AlwaysPersisted) != 0 ? LightingPersistenceMode.AlwaysPersisted : LightingPersistenceMode.CanPersist;
		}
		BrightnessCapabilities? brightnessCapabilities = null;
		PaletteCapabilities? paletteCapabilities = null;
		LightingZoneInformation? unifiedLightingZone = null;
		if ((flags & LightingDeviceFlags.HasBrightness) != 0) brightnessCapabilities = new(reader.ReadByte(), reader.ReadByte());
		if ((flags & LightingDeviceFlags.HasPalette) != 0) paletteCapabilities = new(reader.Read<ushort>());
		if ((flags & LightingDeviceFlags.HasUnifiedLighting) != 0) unifiedLightingZone = ReadLightingZone(ref reader);
		uint count = reader.ReadVariableUInt32();
		LightingZoneInformation[] lightingZones;
		if (count == 0)
		{
			lightingZones = [];
		}
		else
		{
			lightingZones = new LightingZoneInformation[count];
			for (int i = 0; i < lightingZones.Length; i++)
			{
				lightingZones[i] = ReadLightingZone(ref reader);
			}
		}

		var information = new LightingDeviceInformation
		(
			deviceId,
			persistenceMode,
			brightnessCapabilities,
			paletteCapabilities,
			unifiedLightingZone,
			ImmutableCollectionsMarshal.AsImmutableArray(lightingZones)
		);

		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnLightingDeviceUpdate(information));

		static LightingZoneInformation ReadLightingZone(ref BufferReader reader)
		{
			var zoneId = reader.ReadGuid();
			var count = reader.ReadVariableUInt32();
			Guid[] effectIds;
			if (count == 0)
			{
				effectIds = [];
			}
			else
			{
				effectIds = new Guid[count];
				for (int i = 0; i < effectIds.Length; i++)
				{
					effectIds[i] = reader.ReadGuid();
				}
			}
			return new LightingZoneInformation(zoneId, ImmutableCollectionsMarshal.AsImmutableArray(effectIds));
		}
	}

	private void ProcessLightingDeviceConfiguration(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		var deviceId = reader.ReadGuid();
		var flags = (LightingDeviceConfigurationFlags)reader.ReadByte();
		byte? brightness = null;
		RgbColor[] palette;
		if ((flags & LightingDeviceConfigurationFlags.HasBrightness) != 0) brightness = reader.ReadByte();
		if ((flags & LightingDeviceConfigurationFlags.HasPalette) == 0)
		{
			palette = [];
		}
		else
		{
			palette = new RgbColor[reader.ReadVariableUInt32()];
			for (int i = 0; i < palette.Length; i++)
			{
				palette[i] = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
			}
		}
		uint count = reader.ReadVariableUInt32();
		LightingZoneEffect[] lightingZoneEffects;
		if (count == 0)
		{
			lightingZoneEffects = [];
		}
		else
		{
			lightingZoneEffects = new LightingZoneEffect[count];
			for (int i = 0; i < lightingZoneEffects.Length; i++)
			{
				lightingZoneEffects[i] = ReadLightingZoneEffect(ref reader);
			}
		}

		var configuration = new LightingDeviceConfiguration
		(
			deviceId,
			(flags & LightingDeviceConfigurationFlags.IsUnified) != 0,
			brightness,
			ImmutableCollectionsMarshal.AsImmutableArray(palette),
			ImmutableCollectionsMarshal.AsImmutableArray(lightingZoneEffects)
		);

		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnLightingDeviceConfigurationUpdate(configuration));

		static LightingZoneEffect ReadLightingZoneEffect(ref BufferReader reader)
		{
			var zoneId = reader.ReadGuid();
			var effectId = reader.ReadGuid();
			if (effectId == default)
			{
				return new(zoneId, null);
			}
			else
			{
				return new(zoneId, new(effectId, reader.ReadBytes(reader.ReadVariableUInt32())));
			}
		}
	}

	private void ProcessMonitorDevice(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		var information = new MonitorInformation()
		{
			DeviceId = reader.ReadGuid(),
			SupportedSettings = ReadSupportedSettings(ref reader),
			InputSelectSources = ReadValueDescriptions(ref reader),
			InputLagLevels = ReadValueDescriptions(ref reader),
			ResponseTimeLevels = ReadValueDescriptions(ref reader),
			OsdLanguages = ReadValueDescriptions(ref reader),
		};

		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnMonitorDeviceUpdate(information));

		static ImmutableArray<MonitorSetting> ReadSupportedSettings(ref BufferReader reader)
		{
			var count = reader.ReadVariableUInt32();
			if (count == 0) return [];
			var settings = new MonitorSetting[count];
			for (int i = 0; i < settings.Length; i++)
			{
				settings[i] = (MonitorSetting)reader.ReadVariableUInt32();
			}
			return ImmutableCollectionsMarshal.AsImmutableArray(settings);
		}

		static ImmutableArray<NonContinuousValueDescription> ReadValueDescriptions(ref BufferReader reader)
		{
			var count = reader.ReadVariableUInt32();
			if (count == 0) return [];
			var descriptions = new NonContinuousValueDescription[count];
			for (int i = 0; i < descriptions.Length; i++)
			{
				descriptions[i] = ReadValueDescription(ref reader);
			}
			return ImmutableCollectionsMarshal.AsImmutableArray(descriptions);
		}

		static NonContinuousValueDescription ReadValueDescription(ref BufferReader reader)
			=> new(reader.Read<ushort>(), reader.ReadGuid(), reader.ReadVariableString());
	}

	private void ProcessLightingDeviceOperationStatus(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		uint requestId = reader.ReadVariableUInt32();
		var status = (LightingDeviceOperationStatus)reader.ReadByte();

		if (_lightingDeviceOperations is { } lightingDeviceOperations && requestId < (uint)lightingDeviceOperations.Length && Volatile.Read(ref lightingDeviceOperations[requestId]) is { } operation)
			operation.TrySetResult(status);
	}

	private void ProcessMonitorSetting(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		var notification = new MonitorSettingValue
		(
			reader.ReadGuid(),
			(MonitorSetting)reader.ReadVariableUInt32(),
			reader.Read<ushort>(),
			reader.Read<ushort>(),
			reader.Read<ushort>()
		);
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnMonitorSettingUpdate(notification));
	}

	private void ProcessMonitorOperationStatus(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		uint requestId = reader.ReadVariableUInt32();
		var status = (MonitorOperationStatus)reader.ReadByte();

		if (_monitorOperations is { } monitorOperations && requestId < (uint)monitorOperations.Length && Volatile.Read(ref monitorOperations[requestId]) is { } operation)
			operation.TrySetResult(status);
	}

	private void ProcessSensorDevice(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		var deviceId = reader.ReadGuid();
		var sensors = new SensorInformation[reader.ReadVariableUInt32()];

		for (int i = 0; i < sensors.Length; i++)
		{
			var sensorId = reader.ReadGuid();
			var dataType = (SensorDataType)reader.ReadByte();
			var capabilities = (SensorCapabilities)reader.ReadByte();
			string unit = reader.ReadVariableString() ?? "";
			var minimumValue = (capabilities & SensorCapabilities.HasMinimumValue) != 0 ? Read(ref reader, dataType) : default;
			var maximumValue = (capabilities & SensorCapabilities.HasMaximumValue) != 0 ? Read(ref reader, dataType) : default;
			sensors[i] = new(sensorId, dataType, capabilities, unit, minimumValue, maximumValue);
		}

		var info = new SensorDeviceInformation(deviceId, ImmutableCollectionsMarshal.AsImmutableArray(sensors));
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnSensorDeviceUpdate(info));

		static VariantNumber Read(ref BufferReader reader, SensorDataType dataType)
		{
			switch (dataType)
			{
			case SensorDataType.UInt8: return reader.ReadByte();
			case SensorDataType.UInt16: return reader.Read<ushort>();
			case SensorDataType.UInt32: return reader.Read<uint>();
			case SensorDataType.UInt64: return reader.Read<ulong>();
			case SensorDataType.UInt128: return reader.Read<UInt128>();
			case SensorDataType.SInt8: goto case SensorDataType.UInt8;
			case SensorDataType.SInt16: goto case SensorDataType.UInt16;
			case SensorDataType.SInt32: goto case SensorDataType.UInt32;
			case SensorDataType.SInt64: goto case SensorDataType.UInt64;
			case SensorDataType.SInt128: goto case SensorDataType.UInt128;
			case SensorDataType.Float16: goto case SensorDataType.UInt16;
			case SensorDataType.Float32: goto case SensorDataType.UInt32;
			case SensorDataType.Float64: goto case SensorDataType.UInt64;
			default: throw new InvalidOperationException();
			}
		}
	}

	private void ProcessSensorStart(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		var status = (SensorStartStatus)reader.ReadByte();
		if (_sensorWatchOperations is { } sensorWatchOperations && streamId < (uint)sensorWatchOperations.Length && Volatile.Read(ref sensorWatchOperations[streamId]) is { } operation)
			operation.OnStart(status);
	}

	private void ProcessSensorValue(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		if (_sensorWatchOperations is { } sensorWatchOperations && streamId < (uint)sensorWatchOperations.Length && Volatile.Read(ref sensorWatchOperations[streamId]) is { } operation)
			operation.OnValue(data[(int)((uint)data.Length - (uint)reader.RemainingLength)..]);
	}

	private void ProcessSensorConfiguration(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		var deviceId = reader.ReadGuid();
		var sensorId = reader.ReadGuid();
		string? friendlyName = reader.ReadVariableString();
		bool isFavorite = reader.ReadByte() != 0;
		var sensorConfiguration = new SensorConfigurationUpdate() { DeviceId = deviceId, SensorId = sensorId, FriendlyName = friendlyName, IsFavorite = isFavorite };
		_dispatcherQueue.TryEnqueue(() => _serviceClient.OnSensorDeviceConfigurationUpdate(sensorConfiguration));
	}

	private ValueTask<bool> ProcessSensorStopAsync(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		return ProcessSensorStopAsync(streamId);
	}
	private async ValueTask<bool> ProcessSensorStopAsync(uint streamId)
	{
		try
		{
			// We need to acquire the lock because the array needs to be written to.
			using (await WriteLock.WaitAsync(GetDefaultWriteCancellationToken()).ConfigureAwait(false))
				if (_sensorWatchOperations is { } sensorWatchOperations && streamId < (uint)sensorWatchOperations.Length && Interlocked.Exchange(ref _sensorWatchOperations[streamId], null) is { } operation)
					operation.OnStop();
		}
		catch
		{
		}
		return true;
	}


	async ValueTask IMenuItemInvoker.InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken)
	{
		using var cts = CreateWriteCancellationTokenSource(cancellationToken);
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..17];
			FillBuffer(buffer.Span, menuItemId);
			await WriteAsync(buffer, cts.Token).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, Guid menuItemId)
		{
			buffer[0] = (byte)ExoUiProtocolClientMessage.InvokeMenuCommand;
			Unsafe.WriteUnaligned(ref buffer[1], menuItemId);
		}
	}

	async ValueTask ILightingService.SetLightingAsync(LightingDeviceConfigurationUpdate update, CancellationToken cancellationToken)
	{
		TaskCompletionSource<LightingDeviceOperationStatus> operation;
		cancellationToken.ThrowIfCancellationRequested();
		// Not sure if we can use the provided cancellation token to allow cancel writes at all, so for now, resort to the global write cancellation.
		// This shouldn't change much anyway, as pipe write operations should not block for a long time.
		var writeCancellationToken = GetDefaultWriteCancellationToken();
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, writeCancellationToken))
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			// Logic is mostly similar to what is done for sensors, but less complex.
			uint requestId = 0;
			var monitorOperations = _lightingDeviceOperations;
			// NB: It is very unlikely that we would ever have more than two monitor operation running at a time.
			// More than one operation is also unlikely, but it can easily be triggered by switching from the view for one monitor to the view for another monitor, as refreshes are slow.
			if (monitorOperations is null) Volatile.Write(ref _lightingDeviceOperations, monitorOperations = new TaskCompletionSource<LightingDeviceOperationStatus>[2]);
			for (; requestId < monitorOperations.Length; requestId++)
				if (monitorOperations[requestId] is null) break;
			if (requestId >= monitorOperations.Length)
			{
				Array.Resize(ref monitorOperations, (int)(2 * (uint)monitorOperations.Length));
				Volatile.Write(ref _lightingDeviceOperations, monitorOperations);
			}
			operation = new();
			monitorOperations[requestId] = operation;
			var buffer = WriteBuffer;
			Write(buffer.Span, requestId, update);
			// TODO: Find out if cancellation implies that bytes are not written.
			await WriteAsync(buffer, writeCancellationToken).ConfigureAwait(false);
		}
		var status = await operation.Task.ConfigureAwait(false);
		switch (status)
		{
		case LightingDeviceOperationStatus.Success: return;
		case LightingDeviceOperationStatus.DeviceNotFound: throw new DeviceNotFoundException();
		case LightingDeviceOperationStatus.ZoneNotFound: throw new LightingZoneNotFoundException();
		default: throw new InvalidOperationException();
		}

		static void Write(Span<byte> buffer, uint requestId, LightingDeviceConfigurationUpdate update)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.LightingDeviceConfiguration);
			writer.WriteVariable(requestId);
			writer.Write(update.DeviceId);
			var flags = LightingDeviceConfigurationFlags.None;
			if (update.ShouldPersist) flags |= LightingDeviceConfigurationFlags.Persist;
			if (update.BrightnessLevel is not null) flags |= LightingDeviceConfigurationFlags.HasBrightness;
			writer.Write((byte)flags);
			if (update.BrightnessLevel is not null) writer.Write(update.BrightnessLevel.GetValueOrDefault());
			if (update.ZoneEffects.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
			}
			else
			{
				writer.WriteVariable((uint)update.ZoneEffects.Length);
				foreach (var zoneEffect in update.ZoneEffects)
				{
					if (zoneEffect.Effect is null) continue;
					writer.Write(zoneEffect.ZoneId);
					writer.Write(zoneEffect.Effect.EffectId);
					writer.WriteVariable((uint)zoneEffect.Effect.EffectData.Length);
					writer.Write(zoneEffect.Effect.EffectData);
				}
			}
		}
	}

	async ValueTask IMonitorService.SetSettingValueAsync(Guid deviceId, MonitorSetting setting, ushort value, CancellationToken cancellationToken)
	{
		TaskCompletionSource<MonitorOperationStatus> operation;
		cancellationToken.ThrowIfCancellationRequested();
		// Not sure if we can use the provided cancellation token to allow cancel writes at all, so for now, resort to the global write cancellation.
		// This shouldn't change much anyway, as pipe write operations should not block for a long time.
		var writeCancellationToken = GetDefaultWriteCancellationToken();
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, writeCancellationToken))
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			// Logic is mostly similar to what is done for sensors, but less complex.
			uint requestId = 0;
			var monitorOperations = _monitorOperations;
			// NB: It is very unlikely that we would ever have more than two monitor operation running at a time.
			// More than one operation is also unlikely, but it can easily be triggered by switching from the view for one monitor to the view for another monitor, as refreshes are slow.
			if (monitorOperations is null) Volatile.Write(ref _monitorOperations, monitorOperations = new TaskCompletionSource<MonitorOperationStatus>[2]);
			for (; requestId < monitorOperations.Length; requestId++)
				if (monitorOperations[requestId] is null) break;
			if (requestId >= monitorOperations.Length)
			{
				Array.Resize(ref monitorOperations, (int)(2 * (uint)monitorOperations.Length));
				Volatile.Write(ref _monitorOperations, monitorOperations);
			}
			operation = new();
			monitorOperations[requestId] = operation;
			var buffer = WriteBuffer;
			Write(buffer.Span, requestId, deviceId, setting, value);
			// TODO: Find out if cancellation implies that bytes are not written.
			await WriteAsync(buffer, writeCancellationToken).ConfigureAwait(false);
		}
		var status = await operation.Task.ConfigureAwait(false);
		switch (status)
		{
		case MonitorOperationStatus.Success: return;
		case MonitorOperationStatus.DeviceNotFound: throw new DeviceNotFoundException();
		case MonitorOperationStatus.SettingNotFound: throw new SettingNotFoundException();
		default: throw new InvalidOperationException();
		}

		static void Write(Span<byte> buffer, uint requestId, Guid deviceId, MonitorSetting setting, ushort value)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.MonitorSettingSet);
			writer.WriteVariable(requestId);
			writer.Write(deviceId);
			writer.WriteVariable((uint)setting);
			writer.Write(value);
		}
	}

	async ValueTask IMonitorService.RefreshMonitorSettingsAsync(Guid deviceId, CancellationToken cancellationToken)
	{
		TaskCompletionSource<MonitorOperationStatus> operation;
		cancellationToken.ThrowIfCancellationRequested();
		// Not sure if we can use the provided cancellation token to allow cancel writes at all, so for now, resort to the global write cancellation.
		// This shouldn't change much anyway, as pipe write operations should not block for a long time.
		var writeCancellationToken = GetDefaultWriteCancellationToken();
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, writeCancellationToken))
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			// Logic is mostly similar to what is done for sensors, but less complex.
			uint requestId = 0;
			var monitorOperations = _monitorOperations;
			// NB: It is very unlikely that we would ever have more than two monitor operation running at a time.
			// More than one operation is also unlikely, but it can easily be triggered by switching from the view for one monitor to the view for another monitor, as refreshes are slow.
			if (monitorOperations is null) Volatile.Write(ref _monitorOperations, monitorOperations = new TaskCompletionSource<MonitorOperationStatus>[2]);
			for (; requestId < monitorOperations.Length; requestId++)
				if (monitorOperations[requestId] is null) break;
			if (requestId >= monitorOperations.Length)
			{
				Array.Resize(ref monitorOperations, (int)(2 * (uint)monitorOperations.Length));
				Volatile.Write(ref _monitorOperations, monitorOperations);
			}
			operation = new();
			monitorOperations[requestId] = operation;
			var buffer = WriteBuffer;
			Write(buffer.Span, requestId, deviceId);
			// TODO: Find out if cancellation implies that bytes are not written.
			await WriteAsync(buffer, writeCancellationToken).ConfigureAwait(false);
		}
		var status = await operation.Task.ConfigureAwait(false);
		switch (status)
		{
		case MonitorOperationStatus.Success: return;
		case MonitorOperationStatus.DeviceNotFound: throw new DeviceNotFoundException();
		default: throw new InvalidOperationException();
		}

		static void Write(Span<byte> buffer, uint requestId, Guid deviceId)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.MonitorSettingRefresh);
			writer.WriteVariable(requestId);
			writer.Write(deviceId);
		}
	}

	public async Task<SensorWatchOperation<TValue>> WatchSensorValuesAsync<TValue>(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
		where TValue : unmanaged, INumber<TValue>
	{
		Task<SensorWatchOperation<TValue>> startTask;
		cancellationToken.ThrowIfCancellationRequested();
		// Not sure if we can use the provided cancellation token to allow cancel writes at all, so for now, resort to the global write cancellation.
		// This shouldn't change much anyway, as pipe write operations should not block for a long time.
		var writeCancellationToken = GetDefaultWriteCancellationToken();
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, writeCancellationToken))
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			// In many cases, we won't have an excessive amount of sensors, so it is easier to just find any free slot in the (current) array by iterating over it.
			// This can be improved later on if this implementation turns out to be too slow.
			uint streamId = 0;
			var sensorWatchOperations = _sensorWatchOperations;
			if (sensorWatchOperations is null) Volatile.Write(ref _sensorWatchOperations, sensorWatchOperations = new SensorWatchOperation[8]);
			for (; streamId < sensorWatchOperations.Length; streamId++)
				if (sensorWatchOperations[streamId] is null) break;
			if (streamId >= sensorWatchOperations.Length)
			{
				Array.Resize(ref sensorWatchOperations, (int)(2 * (uint)sensorWatchOperations.Length));
				Volatile.Write(ref _sensorWatchOperations, sensorWatchOperations);
			}
			var operation = new SensorWatchOperation<TValue>(this, streamId);
			sensorWatchOperations[streamId] = operation;
			startTask = operation.WaitForStartAsync();
			if (cancellationToken.IsCancellationRequested)
			{
				sensorWatchOperations[streamId] = null;
				cancellationToken.ThrowIfCancellationRequested();
			}
			var buffer = WriteBuffer;
			WriteSensorStart(buffer.Span, streamId, deviceId, sensorId);
			// TODO: Find out if cancellation implies that bytes are not written.
			await WriteAsync(buffer, writeCancellationToken).ConfigureAwait(false);
		}
		return await startTask.ConfigureAwait(false);

		static void WriteSensorStart(Span<byte> buffer, uint streamId, Guid deviceId, Guid sensorId)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.SensorStart);
			writer.WriteVariable(streamId);
			writer.Write(deviceId);
			writer.Write(sensorId);
		}
	}

	internal async ValueTask StopWatchingSensorValuesAsync(uint streamId)
	{
		var cancellationToken = GetDefaultWriteCancellationToken();
		try
		{
			using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				var buffer = WriteBuffer;
				WriteSensorStop(buffer.Span, streamId);
				await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			throw new PipeClosedException();
		}

		static void WriteSensorStop(Span<byte> buffer, uint streamId)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.SensorStop);
			writer.WriteVariable(streamId);
		}
	}


	async ValueTask ISensorService.SetFavoriteAsync(Guid deviceId, Guid sensorId, bool isFavorite, CancellationToken cancellationToken)
	{
		using var cts = CreateWriteCancellationTokenSource(cancellationToken);
		try
		{
			using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
			{
				var buffer = WriteBuffer;
				WriteSensorFavorite(buffer.Span, deviceId, sensorId, isFavorite);
				await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			throw new PipeClosedException();
		}

		static void WriteSensorFavorite(Span<byte> buffer, Guid deviceId, Guid sensorId, bool isFavorite)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.SensorFavorite);
			writer.Write(deviceId);
			writer.Write(sensorId);
			writer.Write(isFavorite ? (byte)1 : (byte)0);
		}
	}

	async IAsyncEnumerable<TValue> ISensorService.WatchValuesAsync<TValue>(Guid deviceId, Guid sensorId, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var operation = await WatchSensorValuesAsync<TValue>(deviceId, sensorId, cancellationToken).ConfigureAwait(false);
		var reader = operation.Reader;
		while (true)
		{
			try
			{
				await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				await operation.DisposeAsync();
				throw;
			}
			while (reader.TryRead(out var value))
				yield return value;
		}
	}
}
