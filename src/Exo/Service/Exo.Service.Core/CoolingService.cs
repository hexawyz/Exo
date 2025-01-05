using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Cooling;
using Exo.Features;
using Exo.Features.Cooling;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal partial class CoolingService
{
	[TypeId(0x74E0B7D0, 0x3CD7, 0x4B85, 0xA5, 0xD4, 0x5E, 0x5B, 0x38, 0xE8, 0xC6, 0xFC)]
	private readonly struct PersistedCoolerInformation
	{
		public PersistedCoolerInformation(CoolerInformation info)
		{
			SensorId = info.SpeedSensorId;
			Type = info.Type;
			SupportedCoolingModes = info.SupportedCoolingModes;
			PowerLimits = info.PowerLimits;
			HardwareCurveInputSensorIds = info.HardwareCurveInputSensorIds;
		}

		public Guid? SensorId { get; }
		public CoolerType Type { get; }
		public CoolingModes SupportedCoolingModes { get; }
		public CoolerPowerLimits? PowerLimits { get; }
		public ImmutableArray<Guid> HardwareCurveInputSensorIds { get; }
	}

	// NB: After thinking about it, this persistence shouldn't be an obstacle for the programming model to come later. (And also it is critically needed)
	// The configuration that is set up at the service level will be considered as some kind of default non-programmable state that will be allowed to be reused within the programming model.
	[TypeId(0x55E60F25, 0x3544, 0x4E42, 0xA2, 0xE8, 0x8E, 0xCC, 0x5A, 0x0A, 0xE1, 0xE1)]
	private readonly struct PersistedCoolerConfiguration
	{
		public required ActiveCoolingMode CoolingMode { get; init; }
	}

	[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "Name", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
	[JsonDerivedType(typeof(AutomaticCoolingMode), "Automatic")]
	[JsonDerivedType(typeof(FixedCoolingMode), "Fixed")]
	[JsonDerivedType(typeof(SoftwareCurveCoolingMode), "SoftwareCurve")]
	[JsonDerivedType(typeof(HardwareCurveCoolingMode), "HardwareCurve")]
	private abstract class ActiveCoolingMode
	{
	}

	private sealed class AutomaticCoolingMode : ActiveCoolingMode { }

	private sealed class FixedCoolingMode : ActiveCoolingMode
	{
		private readonly byte _power;

		[Range(0, 100)]
		public required byte Power
		{
			get => _power;
			init
			{
				ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
				_power = value;
			}
		}
	}

	private sealed class SoftwareCurveCoolingMode : ActiveCoolingMode
	{
		public required Guid SensorDeviceId { get; init; }
		public required Guid SensorId { get; init; }
		private readonly byte _defaultPower;

		[Range(0, 100)]
		public byte DefaultPower
		{
			get => _defaultPower;
			init
			{
				ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
				_defaultPower = value;
			}
		}

		public required PersistedCoolingCurve Curve { get; init; }
	}

	private sealed class HardwareCurveCoolingMode : ActiveCoolingMode
	{
		public required Guid SensorId { get; init; }
		public required PersistedCoolingCurve Curve { get; init; }
	}

	[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "DataType", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
	[JsonDerivedType(typeof(PersistedCoolingCurve<sbyte>), "SInt8")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<byte>), "UInt8")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<short>), "SInt16")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<ushort>), "UInt16")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<int>), "SInt32")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<uint>), "UInt32")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<long>), "SInt64")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<ulong>), "UInt64")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<Half>), "Float16")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<float>), "Float32")]
	[JsonDerivedType(typeof(PersistedCoolingCurve<double>), "Float32")]
	private abstract class PersistedCoolingCurve
	{
	}

	private sealed class PersistedCoolingCurve<T> : PersistedCoolingCurve
		where T : struct, INumber<T>
	{
		private readonly ImmutableArray<DataPoint<T, byte>> _points = [];

		public required ImmutableArray<DataPoint<T, byte>> Points
		{
			get => _points;
			init => _points = value.IsDefaultOrEmpty ? [] : value;
		}
	}

	private static readonly BoundedChannelOptions CoolingChangeChannelOptions = new(20)
	{
		AllowSynchronousContinuations = false,
		FullMode = BoundedChannelFullMode.DropOldest,
		SingleReader = true,
		SingleWriter = false,
	};

	// Helper method that will ensure a cancellation token source is wiped out properly and exactly once. (Because the Dispose method can throw if called twice…)
	private static void ClearAndDisposeCancellationTokenSource(ref CancellationTokenSource? cancellationTokenSource)
	{
		if (Interlocked.Exchange(ref cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			cts.Dispose();
		}
	}

	private const string CoolingConfigurationContainerName = "cln";

	public static async ValueTask<CoolingService> CreateAsync
	(
		ILoggerFactory loggerFactory,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		SensorService sensorService,
		IDeviceWatcher deviceWatcher,
		CancellationToken cancellationToken
	)
	{
		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			if (deviceConfigurationContainer.TryGetContainer(CoolingConfigurationContainerName, GuidNameSerializer.Instance) is not { } coolersConfigurationConfigurationContainer)
			{
				continue;
			}

			var coolerIds = await coolersConfigurationConfigurationContainer.GetKeysAsync(cancellationToken);

			if (coolerIds.Length == 0)
			{
				continue;
			}

			var coolerStates = new Dictionary<Guid, CoolerState>();

			foreach (var coolerId in coolerIds)
			{
				var infoResult = await coolersConfigurationConfigurationContainer.ReadValueAsync<PersistedCoolerInformation>(coolerId, cancellationToken).ConfigureAwait(false);
				if (!infoResult.Found) continue;
				var info = infoResult.Value;
				var configResult = await coolersConfigurationConfigurationContainer.ReadValueAsync<PersistedCoolerConfiguration>(coolerId, cancellationToken).ConfigureAwait(false);
				coolerStates.Add
				(
					coolerId,
					new
					(
						sensorService,
						new CoolerInformation(coolerId, info.SensorId, info.Type, info.SupportedCoolingModes, info.PowerLimits, info.HardwareCurveInputSensorIds)
					)
				);
			}

			if (coolerStates.Count > 0)
			{
				deviceStates.TryAdd
				(
					deviceId,
					new DeviceState
					(
						deviceConfigurationContainer,
						coolersConfigurationConfigurationContainer,
						deviceId,
						coolerStates
					)
				);
			}
		}

		return new CoolingService(loggerFactory, devicesConfigurationContainer, sensorService, deviceWatcher, deviceStates);
	}

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;
	private readonly AsyncLock _lock;
	private ChannelWriter<CoolingDeviceInformation>[]? _changeListeners;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly SensorService _sensorService;
	private readonly ILogger<CoolingService> _logger;
	private readonly IDeviceWatcher _deviceWatcher;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _sensorDeviceWatchTask;

	private CoolingService(ILoggerFactory loggerFactory, IConfigurationContainer<Guid> devicesConfigurationContainer, SensorService sensorService, IDeviceWatcher deviceWatcher, ConcurrentDictionary<Guid, DeviceState> deviceStates)
	{
		_deviceStates = deviceStates;
		_lock = new();
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_sensorService = sensorService;
		_logger = loggerFactory.CreateLogger<CoolingService>();
		_deviceWatcher = deviceWatcher;
		_cancellationTokenSource = new();
		_sensorDeviceWatchTask = WatchSensorsDevicesAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _sensorDeviceWatchTask.ConfigureAwait(false);
			// It is important to stop any background process running as part of the device states here.
			foreach (var state in _deviceStates.Values)
			{
				try
				{
					using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
					{
						await DetachDeviceStateAsync(state).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
			cts.Dispose();
		}
	}

	private async Task WatchSensorsDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<ICoolingDeviceFeature>(cancellationToken))
			{
				try
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Enumeration:
					case WatchNotificationKind.Addition:
						using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							await HandleArrivalAsync(notification, cancellationToken).ConfigureAwait(false);
						}
						break;
					case WatchNotificationKind.Removal:
						using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							// NB: Removal should not be cancelled. We need all the states to be cleared away.
							await HandleRemovalAsync(notification).ConfigureAwait(false);
						}
						break;
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async ValueTask HandleArrivalAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		ImmutableArray<ICooler> coolers;
		var coolingFeatures = (IDeviceFeatureSet<ICoolingDeviceFeature>)notification.FeatureSet!;
		var coolingControllerFeature = coolingFeatures.GetFeature<ICoolingControllerFeature>();
		LiveDeviceState? liveDeviceState;
		Channel<CoolerChange>? changeChannel;
		if (coolingControllerFeature is not null)
		{
			coolers = coolingControllerFeature.Coolers;
			changeChannel = Channel.CreateBounded<CoolerChange>(CoolingChangeChannelOptions);
			liveDeviceState = new LiveDeviceState(coolingControllerFeature, changeChannel);
		}
		else
		{
			coolers = [];
			changeChannel = null;
			liveDeviceState = null;
		}

		try
		{
			var coolerDetails = new (ICooler Cooler, CoolerInformation Info)[coolers.Length];
			// This is used at two places to keep track of cooler IDs.
			// 1 - To identify duplicate IDs
			// 2 - To identify the removed coolers
			var coolerIds = new HashSet<Guid>();
			for (int i = 0; i < coolers.Length; i++)
			{
				var cooler = coolers[i];
				CoolingModes coolingModes = 0;
				var powerLimits = cooler is IConfigurableCooler configurableCooler ?
					new CoolerPowerLimits(configurableCooler.MinimumPower, configurableCooler.CanSwitchOff) :
					null as CoolerPowerLimits?;
				ImmutableArray<Guid> hardwareCurveInputSensorIds = [];
				if (cooler is IAutomaticCooler) coolingModes |= CoolingModes.Automatic;
				if (cooler is IManualCooler) coolingModes |= CoolingModes.Manual;
				if (cooler is IHardwareCurveCooler hardwareCurveCooler)
				{
					coolingModes |= CoolingModes.HardwareControlCurve;
					hardwareCurveInputSensorIds = ImmutableArray.CreateRange(hardwareCurveCooler.AvailableSensors, s => s.SensorId);
				}
				var info = new CoolerInformation(cooler.CoolerId, cooler.SpeedSensorId, cooler.Type, coolingModes, powerLimits, hardwareCurveInputSensorIds);
				if (!coolerIds.Add(cooler.CoolerId))
				{
					// We ignore all sensors and discard the device if there is a duplicate ID.
					// TODO: Log an error.
					coolerDetails = [];
					break;
				}
				coolerDetails[i] = (cooler, info);
			}

			coolerIds.Clear();

			if (coolerDetails.Length == 0)
			{
				if (_deviceStates.TryRemove(notification.DeviceInformation.Id, out var state))
				{
					await state.CoolingConfigurationContainer.DeleteAllContainersAsync().ConfigureAwait(false);
				}
			}
			else
			{
				IConfigurationContainer<Guid> coolingConfigurationContainer;
				Dictionary<Guid, CoolerState> coolerStates;
				if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var state))
				{
					coolerStates = new();

					var deviceContainer = _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id);
					coolingConfigurationContainer = deviceContainer.GetContainer(CoolingConfigurationContainerName, GuidNameSerializer.Instance);

					// For sanity, remove the pre-existing sensor containers, although there should be none initially.
					await coolingConfigurationContainer.DeleteAllContainersAsync().ConfigureAwait(false);
					foreach (var (cooler, info) in coolerDetails)
					{
						var coolerState = new CoolerState(_sensorService, info);
						coolerStates.TryAdd(cooler.CoolerId, coolerState);
						await coolingConfigurationContainer.WriteValueAsync(info.CoolerId, new PersistedCoolerInformation(info), cancellationToken);

						await coolerState.SetOnlineAsync(cooler, info, changeChannel!, cancellationToken).ConfigureAwait(false);
					}

					state = new
					(
						deviceContainer,
						coolingConfigurationContainer,
						notification.DeviceInformation.Id,
						coolerStates
					);

					_deviceStates.TryAdd(notification.DeviceInformation.Id, state);
				}
				else
				{
					coolerStates = state.Coolers;
					coolingConfigurationContainer = state.CoolingConfigurationContainer;

					coolerIds.Clear();

					// Start by identifying the pre-existing coolers by their IDs.
					foreach (var previousCooler in coolerStates.Values)
					{
						coolerIds.Add(previousCooler.Information.CoolerId);
					}

					// Keep track of all coolers that don't exist anymore.
					foreach (var (_, coolerInfo) in coolerDetails)
					{
						coolerIds.Remove(coolerInfo.CoolerId);
					}

					// Clear the state from old coolers.
					foreach (var oldCoolerId in coolerIds)
					{
						if (coolerStates.Remove(oldCoolerId, out var oldCooler))
						{
							// Remove existing sensor configuration if the sensor is not reported by the device anymore.
							await coolingConfigurationContainer.DeleteValuesAsync(oldCooler.Information.CoolerId).ConfigureAwait(false);
						}
					}

					coolerIds.Clear();

					// Finally, mark all of the current sensors as online.
					foreach (var (cooler, info) in coolerDetails)
					{
						if (coolerStates.TryGetValue(info.CoolerId, out var coolerState))
						{
							// Only update the information if it has changed since the last time. (Do not wear the disk with useless writes)
							if (info != coolerState.Information)
							{
								await coolingConfigurationContainer.WriteValueAsync(info.CoolerId, new PersistedCoolerInformation(info), cancellationToken).ConfigureAwait(false);
							}
						}
						else
						{
							coolerState = new CoolerState(_sensorService, info);
							coolerStates.TryAdd(cooler.CoolerId, coolerState);
							await coolingConfigurationContainer.WriteValueAsync(info.CoolerId, new PersistedCoolerInformation(info), cancellationToken);
						}
						await coolerState.SetOnlineAsync(cooler, info, changeChannel!, cancellationToken).ConfigureAwait(false);
					}
				}
				await state.SetOnline(liveDeviceState, cancellationToken).ConfigureAwait(false);
				_changeListeners.TryWrite(new CoolingDeviceInformation(notification.DeviceInformation.Id, ImmutableCollectionsMarshal.AsImmutableArray(Array.ConvertAll(coolerDetails, x => x.Info))));
			}
		}
		catch
		{
			if (liveDeviceState is not null)
			{
				await liveDeviceState.DisposeAsync().ConfigureAwait(false);
			}
			throw;
		}
	}

	private async ValueTask HandleRemovalAsync(DeviceWatchNotification notification)
	{
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var state)) return;

		await DetachDeviceStateAsync(state).ConfigureAwait(false);
	}

	private async ValueTask DetachDeviceStateAsync(DeviceState state)
	{
		await state.SetOfflineAsync(default).ConfigureAwait(false);
	}

	public async IAsyncEnumerable<CoolingDeviceInformation> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<CoolingDeviceInformation>();

		CoolingDeviceInformation[]? initialDeviceInfos = null;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialDeviceInfos = _deviceStates.Values.Select(state => state.CreateInformation()).ToArray();
			ArrayExtensions.InterlockedAdd(ref _changeListeners, channel);
		}
		try
		{
			foreach (var info in initialDeviceInfos)
			{
				yield return info;
			}
			initialDeviceInfos = null;

			await foreach (var info in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return info;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _changeListeners, channel);
		}
	}

	private sealed class CoolerState
	{
		private static readonly object AutomaticPowerState = new();

		private readonly SensorService _sensorService;
		private ICooler? _cooler;
		private ChannelWriter<CoolerChange>? _changeWriter;
		private readonly AsyncLock _lock;
		private object? _activeState;
		private StrongBox<byte>? _manualPowerState;
		private CoolerInformation _information;

		public SensorService SensorService => _sensorService;

		public CoolerState(SensorService sensorService, CoolerInformation information)
		{
			_sensorService = sensorService;
			_lock = new();
			_information = information;
		}

		public CoolerInformation Information => _information;

		public async Task SetOnlineAsync(ICooler cooler, CoolerInformation information, ChannelWriter<CoolerChange> changeWriter, CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(default).ConfigureAwait(false))
			{
				_cooler = cooler;
				_information = information;
				_changeWriter = changeWriter;
				if (cooler is IManualCooler manualCooler)
				{
					if (!manualCooler.TryGetPower(out byte power))
					{
						power = manualCooler.MinimumPower;
					}
					if (_manualPowerState is null)
					{
						_manualPowerState = new(power);
					}
				}
				else
				{
					_manualPowerState = null;
				}
			}
		}

		public async Task SetOfflineAsync(CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(default).ConfigureAwait(false))
			{
				_cooler = null;
				_changeWriter = null;
				if (_activeState is DynamicCoolerState dynamicState)
				{
					await dynamicState.StopAsync().ConfigureAwait(false);
				}
			}
		}

		public async ValueTask RestoreStateAsync(CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				_cooler = null;
				var activeState = _activeState;
				if (activeState is DynamicCoolerState dynamicState)
				{
					dynamicState.Reset();
					dynamicState.Start();
				}
				else if (activeState is HardwareCurveCoolerState hardwareCurveState)
				{
					_changeWriter!.TryWrite(hardwareCurveState.CreateCoolerChange());
				}
				else if (_manualPowerState is not null && ReferenceEquals(activeState, _manualPowerState))
				{
					SendManualPowerUpdate(_manualPowerState.Value);
				}
			}
		}

		public async ValueTask<PersistedCoolerConfiguration?> CreatePersistedConfigurationAsync(CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				var activeState = _activeState;
				ActiveCoolingMode activeCoolingMode;
				if (_activeState is DynamicCoolerState dynamicState)
				{
					activeCoolingMode = dynamicState.GetPersistedConfiguration();
				}
				else if (activeState is HardwareCurveCoolerState hardwareCurveState)
				{
					activeCoolingMode = hardwareCurveState.GetPersistedConfiguration();
				}
				else if (_manualPowerState is not null && ReferenceEquals(_activeState, _manualPowerState))
				{
					activeCoolingMode = new FixedCoolingMode() { Power = _manualPowerState.Value };
				}
				else
				{
					return null;
				}
				return new() { CoolingMode = activeCoolingMode };
			}
		}

		public async ValueTask SetAutomaticPowerAsync(CancellationToken cancellationToken)
		{
			if (_cooler is not IAutomaticCooler automaticCooler) throw new InvalidOperationException("Automatic cooling is not supported by this cooler.");

			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (ReferenceEquals(_activeState, AutomaticPowerState)) return;
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				_changeWriter!.TryWrite(CoolerChange.CreateAutomatic(automaticCooler));
				_activeState = AutomaticPowerState;
			}
		}

		public async ValueTask SetManualPowerAsync(byte power, CancellationToken cancellationToken)
		{
			if (_manualPowerState is null) throw new InvalidOperationException("Manual cooling is not supported by this cooler.");

			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_manualPowerState.Value != power) _manualPowerState.Value = power;
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				else if (!ReferenceEquals(_activeState, _manualPowerState)) _activeState = AutomaticPowerState;
				SendManualPowerUpdate(power);
			}
		}

		public async ValueTask SetDynamicPowerAsyncAsync<TInput>
		(
			Guid sensorDeviceId,
			Guid sensorId,
			byte fallbackValue,
			InterpolatedSegmentControlCurve<TInput, byte> controlCurve,
			CancellationToken cancellationToken
		)
			where TInput : struct, INumber<TInput>
		{
			if (_manualPowerState is null) throw new InvalidOperationException("Manual cooling is not supported by this cooler.");

			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				var dynamicCoolerState = new DynamicCoolerState<TInput>(this, sensorDeviceId, sensorId, fallbackValue, controlCurve);
				dynamicCoolerState.Start();
				_activeState = dynamicCoolerState;
			}
		}

		public async ValueTask SetHardwareCurveAsync<TInput>(Guid sensorId, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
			where TInput : struct, INumber<TInput>
		{
			if (_cooler is not IHardwareCurveCooler cooler) throw new InvalidOperationException("Hardware cooling curves are not supported by this cooler.");
			var sensor = FindSensor(cooler, sensorId);

			switch (SensorService.GetSensorDataType(sensor.ValueType))
			{
			case SensorDataType.UInt8:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<byte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt16:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<ushort>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt32:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<uint>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt64:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<ulong>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt128:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<UInt128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt8:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<sbyte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt16:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<short>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt32:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<int>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt64:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<long>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt128:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<Int128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float16:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<Half>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float32:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<float>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float64:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<double>(), cancellationToken).ConfigureAwait(false);
				break;
			default:
				throw new InvalidOperationException("Unsupported sensor data type.");
			}
		}

		private async ValueTask SetHardwareCurveAsync<TInput>(IHardwareCurveCoolerSensorCurveControl sensor, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
			where TInput : struct, INumber<TInput>
		{
			if (sensor is not IHardwareCurveCoolerSensorCurveControl<TInput> typedSensor) throw new InvalidOperationException("This sensor has an incompatible value type.");
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				var hardwareCurveState = new HardwareCurveCoolerState<TInput>(typedSensor, controlCurve);
				_activeState = hardwareCurveState;
				_changeWriter!.TryWrite(hardwareCurveState.CreateCoolerChange());
			}
		}

		private static IHardwareCurveCoolerSensorCurveControl FindSensor(IHardwareCurveCooler cooler, Guid sensorId)
		{
			foreach (var sensor in cooler.AvailableSensors)
			{
				if (sensor.SensorId == sensorId) return sensor;
			}
			throw new InvalidOperationException("The specified sensor ID is not a valid hardware control curve input source.");
		}

		public void SendManualPowerUpdate(byte power) => _changeWriter!.TryWrite(CoolerChange.CreateManual(Unsafe.As<IManualCooler>(_cooler!), power));
	}

	private static PersistedCoolingCurve<TInput> CreatePersistedCurve<TInput>(InterpolatedSegmentControlCurve<TInput, byte> controlCurve)
		where TInput : struct, INumber<TInput>
		=> new PersistedCoolingCurve<TInput>() { Points = controlCurve.GetPoints() };

	private abstract class HardwareCurveCoolerState
	{
		public abstract CoolerChange CreateCoolerChange();
		public abstract HardwareCurveCoolingMode GetPersistedConfiguration();
	}

	private sealed class HardwareCurveCoolerState<TInput> : HardwareCurveCoolerState
		where TInput : struct, INumber<TInput>
	{
		private readonly IHardwareCurveCoolerSensorCurveControl<TInput> _sensor;
		private readonly InterpolatedSegmentControlCurve<TInput, byte> _controlCurve;

		public HardwareCurveCoolerState(IHardwareCurveCoolerSensorCurveControl<TInput> sensor, InterpolatedSegmentControlCurve<TInput, byte> controlCurve)
		{
			_sensor = sensor;
			_controlCurve = controlCurve;
		}

		public override CoolerChange CreateCoolerChange() => CoolerChange.CreateHardwareCurve(_sensor, _controlCurve);

		public override HardwareCurveCoolingMode GetPersistedConfiguration()
			=> new HardwareCurveCoolingMode()
			{
				SensorId = _sensor.SensorId,
				Curve = CreatePersistedCurve(_controlCurve)
			};
	}

	private abstract class DynamicCoolerState
	{
		private readonly CoolerState _coolerState;
		private CancellationTokenSource? _cancellationTokenSource;
		private Task? _runTask;

		protected CoolerState CoolerState => _coolerState;

		protected DynamicCoolerState(CoolerState coolerState)
		{
			_coolerState = coolerState;
			_cancellationTokenSource = new();
		}

		internal void Start()
		{
			if (_cancellationTokenSource is null) throw new InvalidOperationException();
			if (_runTask is not null) throw new InvalidOperationException();
			_runTask = RunAsync(_cancellationTokenSource!.Token);
		}

		public async ValueTask StopAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
			cts.Cancel();
			if (_runTask is not null)
			{
				await _runTask.ConfigureAwait(false);
			}
			cts.Dispose();
		}

		/// <summary>Resets the state, after it has been stopped.</summary>
		/// <remarks>
		/// This operation is important to allow for restarting a dynamic state after a device came back online from a previous time.
		/// This is actually a niche use case as cooling devices would generally be always on. However, devices can go offline for many reasons, so we definitely want to have this working.
		/// </remarks>
		/// <returns></returns>
		internal void Reset()
		{
			if (Interlocked.CompareExchange(ref _cancellationTokenSource, new CancellationTokenSource(), null) is not null) throw new InvalidOperationException();
		}

		/// <summary>Runs this dynamic state until it is requested to stop.</summary>
		/// <remarks>This method can be called multiple times but never more than once at a time.</remarks>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		protected abstract Task RunAsync(CancellationToken cancellationToken);

		public abstract SoftwareCurveCoolingMode GetPersistedConfiguration();
	}

	private sealed class DynamicCoolerState<TInput> : DynamicCoolerState
		where TInput : struct, INumber<TInput>
	{
		private readonly InterpolatedSegmentControlCurve<TInput, byte> _controlCurve;
		private readonly Guid _sensorDeviceId;
		private readonly Guid _sensorId;
		private readonly byte _fallbackValue;

		public DynamicCoolerState(CoolerState coolerState, Guid sensorDeviceId, Guid sensorId, byte fallbackValue, InterpolatedSegmentControlCurve<TInput, byte> controlCurve) : base(coolerState)
		{
			_sensorDeviceId = sensorDeviceId;
			_sensorId = sensorId;
			_fallbackValue = fallbackValue;
			_controlCurve = controlCurve;
		}

		protected override async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					// Always reset the power to the fallback value when the sensor value is unknown.
					// This can have downsides, but if well configured, it can also avoid catastrophic failures when the sensor has become unavailable for some reason.
					CoolerState.SendManualPowerUpdate(_fallbackValue);
					await CoolerState.SensorService.WaitForSensorAsync(_sensorDeviceId, _sensorId, cancellationToken).ConfigureAwait(false);
					try
					{
						await foreach (var dataPoint in CoolerState.SensorService.WatchValuesAsync<TInput>(_sensorDeviceId, _sensorId, cancellationToken).ConfigureAwait(false))
						{
							// NB: The state lock is not acquired here, as we are guaranteed that this dynamic state will be disposed before any other update can occur.
							CoolerState.SendManualPowerUpdate(_controlCurve[dataPoint.Value]);
						}
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
					}
					catch (DeviceDisconnectedException)
					{
						// TODO: Log (Information)
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				// Reset the power to the fallback value in case of error, as the sensor value is now technically unknown.
				CoolerState.SendManualPowerUpdate(_fallbackValue);
				// TODO: Log (Error)
			}
		}

		public override SoftwareCurveCoolingMode GetPersistedConfiguration()
			=> new SoftwareCurveCoolingMode()
			{
				SensorDeviceId = _sensorDeviceId,
				SensorId = _sensorId,
				DefaultPower = _fallbackValue,
				Curve = CreatePersistedCurve(_controlCurve)
			};
	}

	public async ValueTask SetAutomaticPowerAsync(Guid deviceId, Guid coolerId, CancellationToken cancellationToken)
	{
		if (TryGetCoolerState(deviceId, coolerId, out var state))
		{
			await state.SetAutomaticPowerAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask SetFixedPowerAsync(Guid deviceId, Guid coolerId, byte power, CancellationToken cancellationToken)
	{
		if (TryGetCoolerState(deviceId, coolerId, out var state))
		{
			await state.SetManualPowerAsync(power, cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask SetSoftwareControlCurveAsync<TInput>(Guid coolingDeviceId, Guid coolerId, Guid sensorDeviceId, Guid sensorId, byte fallbackValue, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
		where TInput : struct, INumber<TInput>
	{
		var sensorInformation = await _sensorService.GetSensorInformationAsync(sensorDeviceId, sensorId, cancellationToken).ConfigureAwait(false);

		if (TryGetCoolerState(coolingDeviceId, coolerId, out var state))
		{
			switch (sensorInformation.DataType)
			{
			case SensorDataType.UInt8:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<byte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt16:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<ushort>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt32:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<uint>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt64:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<ulong>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt128:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<UInt128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt8:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<sbyte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt16:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<short>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt32:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<int>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt64:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<long>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt128:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<Int128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float16:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<Half>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float32:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<float>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float64:
				await state.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<double>(), cancellationToken).ConfigureAwait(false);
				break;
			default:
				throw new InvalidOperationException("Unsupported sensor data type.");
			}
		}
	}

	public async ValueTask SetHardwareControlCurveAsync<TInput>(Guid coolingDeviceId, Guid coolerId, Guid sensorId, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
		where TInput : struct, INumber<TInput>
	{
		if (TryGetCoolerState(coolingDeviceId, coolerId, out var state))
		{
			await state.SetHardwareCurveAsync(sensorId, controlCurve, cancellationToken).ConfigureAwait(false);
		}
	}

	private bool TryGetCoolerState(Guid deviceId, Guid coolerId, [NotNullWhen(true)] out CoolerState? state)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			if (deviceState.Coolers is { } coolerStates)
			{
				return coolerStates.TryGetValue(coolerId, out state);
			}
		}
		state = null;
		return false;
	}
}
