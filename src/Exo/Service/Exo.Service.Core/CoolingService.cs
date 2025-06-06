using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Cooling;
using Exo.Cooling.Configuration;
using Exo.Features;
using Exo.Features.Cooling;
using Exo.Primitives;
using Exo.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal partial class CoolingService : IChangeSource<CoolingDeviceInformation>, IChangeSource<CoolingUpdate>
{
	private static readonly BoundedChannelOptions CoolingChangeChannelOptions = new(20)
	{
		AllowSynchronousContinuations = false,
		FullMode = BoundedChannelFullMode.DropOldest,
		SingleReader = true,
		SingleWriter = false,
	};

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
				var infoResult = await coolersConfigurationConfigurationContainer.ReadValueAsync(coolerId, SourceGenerationContext.Default.PersistedCoolerInformation, cancellationToken).ConfigureAwait(false);
				if (!infoResult.Found) continue;
				var info = infoResult.Value;
				var configResult = await coolersConfigurationConfigurationContainer.ReadValueAsync(coolerId, SourceGenerationContext.Default.CoolerConfiguration, cancellationToken).ConfigureAwait(false);
				coolerStates.Add
				(
					coolerId,
					new
					(
						sensorService,
						new CoolerInformation(coolerId, info.SensorId, info.Type, info.SupportedCoolingModes, info.PowerLimits, info.HardwareCurveInputSensorIds),
						configResult.Found ? configResult.Value : null
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
	private ChangeBroadcaster<CoolingDeviceInformation> _deviceChangeBroadcaster;
	private ChangeBroadcaster<CoolingUpdate> _coolingChangeBroadcaster;
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
					await DetachDeviceStateAsync(state).ConfigureAwait(false);
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
						var coolerState = new CoolerState(_sensorService, info, null);
						coolerStates.TryAdd(cooler.CoolerId, coolerState);
						await coolingConfigurationContainer.WriteValueAsync(info.CoolerId, new PersistedCoolerInformation(info), SourceGenerationContext.Default.PersistedCoolerInformation, cancellationToken);

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
								await coolingConfigurationContainer.WriteValueAsync(info.CoolerId, new PersistedCoolerInformation(info), SourceGenerationContext.Default.PersistedCoolerInformation, cancellationToken).ConfigureAwait(false);
							}
						}
						else
						{
							coolerState = new CoolerState(_sensorService, info, null);
							coolerStates.TryAdd(cooler.CoolerId, coolerState);
							await coolingConfigurationContainer.WriteValueAsync(info.CoolerId, new PersistedCoolerInformation(info), SourceGenerationContext.Default.PersistedCoolerInformation, cancellationToken);
						}
						await coolerState.SetOnlineAsync(cooler, info, changeChannel!, cancellationToken).ConfigureAwait(false);
					}
				}
				await state.SetOnlineAsync(liveDeviceState, cancellationToken).ConfigureAwait(false);
				_deviceChangeBroadcaster.Push(new CoolingDeviceInformation(notification.DeviceInformation.Id, ImmutableCollectionsMarshal.AsImmutableArray(Array.ConvertAll(coolerDetails, x => x.Info))));
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

	async ValueTask<CoolingDeviceInformation[]?> IChangeSource<CoolingDeviceInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<CoolingDeviceInformation> writer, CancellationToken cancellationToken)
	{
		List<CoolingDeviceInformation>? initialDeviceInfos = null;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var device in _deviceStates.Values)
			{
				(initialDeviceInfos ??= new()).Add(device.CreateInformation());
			}
			_deviceChangeBroadcaster.Register(writer);
		}
		return initialDeviceInfos?.ToArray();
	}

	void IChangeSource<CoolingDeviceInformation>.UnsafeUnregisterWatcher(ChannelWriter<CoolingDeviceInformation> writer)
		=> _deviceChangeBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<CoolingDeviceInformation>.SafeUnregisterWatcherAsync(ChannelWriter<CoolingDeviceInformation> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_deviceChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	async ValueTask<CoolingUpdate[]?> IChangeSource<CoolingUpdate>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<CoolingUpdate> writer, CancellationToken cancellationToken)
	{
		List<CoolingUpdate>? initialUpdates = null;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var device in _deviceStates.Values)
			{
				foreach (var cooler in device.Coolers.Values)
				{
					if (cooler.CreatePersistedConfiguration() is { } configuration)
					{
						(initialUpdates ??= new()).Add(new CoolingUpdate(device.DeviceId, cooler.Information.CoolerId, configuration.CoolingMode));
					}
				}
			}
			_coolingChangeBroadcaster.Register(writer);
		}
		return initialUpdates?.ToArray();
	}

	void IChangeSource<CoolingUpdate>.UnsafeUnregisterWatcher(ChannelWriter<CoolingUpdate> writer)
		=> _coolingChangeBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<CoolingUpdate>.SafeUnregisterWatcherAsync(ChannelWriter<CoolingUpdate> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_coolingChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	private static CoolingControlCurveConfiguration<TInput> CreatePersistedCurve<TInput>(InterpolatedSegmentControlCurve<TInput, byte> controlCurve)
		where TInput : struct, INumber<TInput>
	{
		var (initialValue, points) = controlCurve.GetData();
		return new(points, initialValue);
	}

	public async ValueTask SetAutomaticPowerAsync(Guid deviceId, Guid coolerId, CancellationToken cancellationToken)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState) && deviceState.Coolers is { } coolerStates && coolerStates.TryGetValue(coolerId, out var coolerState))
		{
			await coolerState.SetAutomaticPowerAsync(cancellationToken).ConfigureAwait(false);
			PersistCoolingConfiguration(deviceState, coolerState, cancellationToken);
		}
	}

	public async ValueTask SetFixedPowerAsync(Guid deviceId, Guid coolerId, byte power, CancellationToken cancellationToken)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState) && deviceState.Coolers is { } coolerStates && coolerStates.TryGetValue(coolerId, out var coolerState))
		{
			await coolerState.SetManualPowerAsync(power, cancellationToken).ConfigureAwait(false);
			PersistCoolingConfiguration(deviceState, coolerState, cancellationToken);
		}
	}

	public async ValueTask SetSoftwareControlCurveAsync<TInput>(Guid coolingDeviceId, Guid coolerId, Guid sensorDeviceId, Guid sensorId, byte fallbackValue, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
		where TInput : struct, INumber<TInput>
	{
		var sensorInformation = await _sensorService.GetSensorInformationAsync(sensorDeviceId, sensorId, cancellationToken).ConfigureAwait(false);

		if (_deviceStates.TryGetValue(coolingDeviceId, out var deviceState) && deviceState.Coolers is { } coolerStates && coolerStates.TryGetValue(coolerId, out var coolerState))
		{
			switch (sensorInformation.DataType)
			{
			case SensorDataType.UInt8:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<byte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt16:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<ushort>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt32:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<uint>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt64:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<ulong>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt128:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<UInt128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt8:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<sbyte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt16:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<short>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt32:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<int>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt64:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<long>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt128:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<Int128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float16:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<Half>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float32:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<float>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float64:
				await coolerState.SetDynamicPowerAsyncAsync(sensorDeviceId, sensorId, fallbackValue, controlCurve.CastInput<double>(), cancellationToken).ConfigureAwait(false);
				break;
			default:
				throw new InvalidOperationException("Unsupported sensor data type.");
			}
			PersistCoolingConfiguration(deviceState, coolerState, cancellationToken);
		}
	}

	public async ValueTask SetHardwareControlCurveAsync<TInput>(Guid coolingDeviceId, Guid coolerId, Guid sensorId, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
		where TInput : struct, INumber<TInput>
	{
		if (_deviceStates.TryGetValue(coolingDeviceId, out var deviceState) && deviceState.Coolers is { } coolerStates && coolerStates.TryGetValue(coolerId, out var coolerState))
		{
			await coolerState.SetHardwareCurveAsync(sensorId, controlCurve, cancellationToken).ConfigureAwait(false);
			PersistCoolingConfiguration(deviceState, coolerState, cancellationToken);
		}
	}

	private void PersistCoolingConfiguration(DeviceState deviceState, CoolerState coolerState, CancellationToken cancellationToken)
	{
		if (coolerState.CreatePersistedConfiguration() is { } configuration)
		{
			PersistCoolingConfiguration(deviceState.CoolingConfigurationContainer, coolerState.Information.CoolerId, configuration, cancellationToken);
			var coolingChangeBroadcaster = _coolingChangeBroadcaster.GetSnapshot();
			if (!coolingChangeBroadcaster.IsEmpty)
			{
				coolingChangeBroadcaster.Push(new(deviceState.DeviceId, coolerState.Information.CoolerId, configuration.CoolingMode));
			}
		}
	}

	private async void PersistCoolingConfiguration(IConfigurationContainer<Guid> coolersConfigurationContainer, Guid coolerId, CoolerConfiguration configuration, CancellationToken cancellationToken)
	{
		try
		{
			await PersistCoolingConfigurationAsync(coolersConfigurationContainer, coolerId, configuration, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	private ValueTask PersistCoolingConfigurationAsync(IConfigurationContainer<Guid> coolersConfigurationContainer, Guid coolerId, CoolerConfiguration configuration, CancellationToken cancellationToken)
		=> coolersConfigurationContainer.WriteValueAsync(coolerId, configuration, SourceGenerationContext.Default.CoolerConfiguration, cancellationToken);
}
