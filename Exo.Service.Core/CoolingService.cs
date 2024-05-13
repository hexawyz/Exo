using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
		}

		public Guid? SensorId { get; }
		public CoolerType Type { get; }
		public CoolingModes SupportedCoolingModes { get; }
	}

	// TODO: See if we want to persist the settings. This might conflict with plans for the programming service.
	//[TypeId(0x55E60F25, 0x3544, 0x4E42, 0xA2, 0xE8, 0x8E, 0xCC, 0x5A, 0x0A, 0xE1, 0xE1)]
	//private readonly struct PersistedCoolerConfiguration
	//{
	//	public CoolingMode CoolingMode { get; }
	//}

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

			var collerIds = await coolersConfigurationConfigurationContainer.GetKeysAsync(cancellationToken);

			if (collerIds.Length == 0)
			{
				continue;
			}

			var coolerInformations = ImmutableArray.CreateBuilder<CoolerInformation>();

			foreach (var coolerId in collerIds)
			{
				var result = await coolersConfigurationConfigurationContainer.ReadValueAsync<PersistedCoolerInformation>(coolerId, cancellationToken).ConfigureAwait(false);
				if (!result.Found) continue;
				var info = result.Value;
				coolerInformations.Add(new CoolerInformation(coolerId, info.SensorId, info.Type, info.SupportedCoolingModes));
			}

			if (coolerInformations.Count > 0)
			{
				deviceStates.TryAdd
				(
					deviceId,
					new DeviceState
					(
						deviceConfigurationContainer,
						coolersConfigurationConfigurationContainer,
						false,
						new(deviceId, coolerInformations.DrainToImmutable()),
						null,
						null
					)
				);
			}
		}

		return new CoolingService(loggerFactory, devicesConfigurationContainer, deviceWatcher, deviceStates);
	}

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;
	private readonly AsyncLock _lock;
	private ChannelWriter<CoolingDeviceInformation>[]? _changeListeners;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly ILogger<CoolingService> _logger;
	private readonly IDeviceWatcher _deviceWatcher;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _sensorDeviceWatchTask;

	private CoolingService(ILoggerFactory loggerFactory, IConfigurationContainer<Guid> devicesConfigurationContainer, IDeviceWatcher deviceWatcher, ConcurrentDictionary<Guid, DeviceState> deviceStates)
	{
		_deviceStates = deviceStates;
		_lock = new();
		_devicesConfigurationContainer = devicesConfigurationContainer;
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
		Channel<CoolerState>? changeChannel;
		if (coolingControllerFeature is not null)
		{
			coolers = coolingControllerFeature.Coolers;
			changeChannel = Channel.CreateBounded<CoolerState>(CoolingChangeChannelOptions);
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
			var coolerInfos = new CoolerInformation[coolers.Length];
			var coolerStates = new Dictionary<Guid, CoolerState>();
			var addedCoolerInfosById = new Dictionary<Guid, CoolerInformation>();
			for (int i = 0; i < coolers.Length; i++)
			{
				var cooler = coolers[i];
				if (!coolerStates.TryAdd(cooler.CoolerId, new(cooler)))
				{
					// We ignore all sensors and discard the device if there is a duplicate ID.
					// TODO: Log an error.
					coolerInfos = [];
					coolerStates.Clear();
					break;
				}
				CoolingModes coolingModes = 0;
				if (cooler is IAutomaticCooler) coolingModes |= CoolingModes.Automatic;
				if (cooler is IManualCooler) coolingModes |= CoolingModes.Manual;
				var info = new CoolerInformation(cooler.CoolerId, cooler.SpeedSensorId, cooler.Type, coolingModes);
				addedCoolerInfosById.Add(info.CoolerId, info);
				coolerInfos[i] = info;
			}

			if (coolerInfos.Length == 0)
			{
				if (_deviceStates.TryRemove(notification.DeviceInformation.Id, out var state))
				{
					await state.CoolingConfigurationContainer.DeleteAllContainersAsync().ConfigureAwait(false);
				}
			}
			else
			{
				IConfigurationContainer<Guid> coolingConfigurationContainer;
				if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var state))
				{
					var deviceContainer = _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id);
					coolingConfigurationContainer = deviceContainer.GetContainer(CoolingConfigurationContainerName, GuidNameSerializer.Instance);

					// For sanity, remove the pre-existing sensor containers, although there should be none initially.
					await coolingConfigurationContainer.DeleteAllContainersAsync().ConfigureAwait(false);
					foreach (var info in coolerInfos)
					{
						await coolingConfigurationContainer.WriteValueAsync(info.CoolerId, new PersistedCoolerInformation(info), cancellationToken);
					}

					state = new
					(
						deviceContainer,
						coolingConfigurationContainer,
						notification.DeviceInformation.IsAvailable,
						new(notification.DeviceInformation.Id, ImmutableCollectionsMarshal.AsImmutableArray(coolerInfos)),
						liveDeviceState,
						coolerStates
					);

					_deviceStates.TryAdd(notification.DeviceInformation.Id, state);
				}
				else
				{
					coolingConfigurationContainer = state.CoolingConfigurationContainer;

					foreach (var previousInfo in state.Information.Coolers)
					{
						// Remove all pre-existing sensor info from the dictionary that was build earlier so that only new entries remain in the end.
						// Appropriate updates for previous sensors will be done depending on the result of that removal.
						if (!addedCoolerInfosById.Remove(previousInfo.CoolerId, out var currentInfo))
						{
							// Remove existing sensor configuration if the sensor is not reported by the device anymore.
							await coolingConfigurationContainer.DeleteValuesAsync(previousInfo.CoolerId).ConfigureAwait(false);
						}
						else if (currentInfo != previousInfo)
						{
							// Only update the information if it has changed since the last time. (Do not wear the disk with useless writes)
							await coolingConfigurationContainer.WriteValueAsync(currentInfo.CoolerId, new PersistedCoolerInformation(currentInfo), cancellationToken).ConfigureAwait(false);
						}
					}

					// Finally, persist the information for the newly discovered sensors.
					foreach (var currentInfo in addedCoolerInfosById.Values)
					{
						await coolingConfigurationContainer.WriteValueAsync(currentInfo.CoolerId, new PersistedCoolerInformation(currentInfo), cancellationToken).ConfigureAwait(false);
					}

					using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
					{
						state.Information = new CoolingDeviceInformation(notification.DeviceInformation.Id, ImmutableCollectionsMarshal.AsImmutableArray(coolerInfos));
						state.LiveDeviceState = liveDeviceState;
						state.CoolerStates = coolerStates;
						state.IsConnected = notification.DeviceInformation.IsAvailable;
					}
				}
				_changeListeners.TryWrite(state.Information);
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
		using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
		{
			state.IsConnected = false;
			if (state.LiveDeviceState is { } liveDeviceState)
			{
				await liveDeviceState.DisposeAsync().ConfigureAwait(false);
			}
			state.CoolerStates = null;
		}
	}

	public async IAsyncEnumerable<CoolingDeviceInformation> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<CoolingDeviceInformation>();

		CoolingDeviceInformation[]? initialDeviceInfos = null;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialDeviceInfos = _deviceStates.Values.Select(state => state.Information).ToArray();
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

	private class CoolerState
	{
		private readonly ICooler _cooler;

		public CoolerState(ICooler cooler) => _cooler = cooler;
	}
}
