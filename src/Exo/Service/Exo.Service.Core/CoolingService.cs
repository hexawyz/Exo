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
using Exo.Sensors;
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
		public CoolerPowerLimits? PowerLimits { get; }
	}

	// TODO: Write the persistance code.
	// NB: After thinking about it, this shouldn't be an obstacle for the programming model to come later.
	// The configuration that is set up at the service level will be considered as some kind of default non-programmable state that will be allowed to be reused within the programmation model.
	[TypeId(0x55E60F25, 0x3544, 0x4E42, 0xA2, 0xE8, 0x8E, 0xCC, 0x5A, 0x0A, 0xE1, 0xE1)]
	private abstract class PersistedCoolerConfiguration
	{
		public required ActiveCoolingMode CoolingMode { get; init; }
	}

	[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "Name", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
	[JsonDerivedType(typeof(AutomaticCoolingMode), "Automatic")]
	[JsonDerivedType(typeof(FixedCoolingMode), "Fixed")]
	private abstract class ActiveCoolingMode
	{
	}

	private sealed class AutomaticCoolingMode { }

	private sealed class FixedCoolingMode
	{
		private readonly byte _power;

		[Range(0, 100)]
		public byte Power
		{
			get => _power;
			init
			{
				ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
				_power = value;
			}
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
				coolerInformations.Add(new CoolerInformation(coolerId, info.SensorId, info.Type, info.SupportedCoolingModes, info.PowerLimits));
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
			var coolerInfos = new CoolerInformation[coolers.Length];
			var coolerStates = new Dictionary<Guid, CoolerState>();
			var addedCoolerInfosById = new Dictionary<Guid, CoolerInformation>();
			for (int i = 0; i < coolers.Length; i++)
			{
				var cooler = coolers[i];
				if (!coolerStates.TryAdd(cooler.CoolerId, new(_sensorService, cooler, changeChannel!)))
				{
					// We ignore all sensors and discard the device if there is a duplicate ID.
					// TODO: Log an error.
					coolerInfos = [];
					coolerStates.Clear();
					break;
				}
				CoolingModes coolingModes = 0;
				var powerLimits = cooler is IConfigurableCooler configurableCooler ?
					new CoolerPowerLimits(configurableCooler.MinimumPower, configurableCooler.CanSwitchOff) :
					null as CoolerPowerLimits?;
				if (cooler is IAutomaticCooler) coolingModes |= CoolingModes.Automatic;
				if (cooler is IManualCooler) coolingModes |= CoolingModes.Manual;
				var info = new CoolerInformation(cooler.CoolerId, cooler.SpeedSensorId, cooler.Type, coolingModes, powerLimits);
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

	private sealed class CoolerState
	{
		private static readonly object AutomaticPowerState = new();

		private readonly SensorService _sensorService;
		private readonly ICooler _cooler;
		private readonly ChannelWriter<CoolerChange> _changeWriter;
		private readonly AsyncLock _lock;
		private object? _activeState;
		private readonly StrongBox<int>? _manualPowerState;

		public SensorService SensorService => _sensorService;

		public CoolerState(SensorService sensorService, ICooler cooler, ChannelWriter<CoolerChange> changeWriter)
		{
			_sensorService = sensorService;
			_cooler = cooler;
			_changeWriter = changeWriter;
			_lock = new();
			if (cooler is IManualCooler manualCooler)
			{
				if (!manualCooler.TryGetPower(out byte power))
				{
					power = manualCooler.MinimumPower;
				}
				_manualPowerState = new(power);
			}
		}

		public async ValueTask SetAutomaticPowerAsync(CancellationToken cancellationToken)
		{
			if (_cooler is not IAutomaticCooler automaticCooler) throw new InvalidOperationException("Automatic cooling is not supported by this cooler.");

			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (ReferenceEquals(_activeState, AutomaticPowerState)) return;
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				_changeWriter.TryWrite(CoolerChange.CreateAutomatic(automaticCooler));
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

		public void SendManualPowerUpdate(byte power) => _changeWriter.TryWrite(CoolerChange.CreateManual(Unsafe.As<IManualCooler>(_cooler), power));
	}

	private abstract class DynamicCoolerState : IAsyncDisposable
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
			ObjectDisposedException.ThrowIf(_cancellationTokenSource is null, typeof(DynamicCoolerState));
			if (_runTask is not null) throw new InvalidOperationException();
			_runTask = RunAsync(_cancellationTokenSource!.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
			cts.Cancel();
			if (_runTask is not null)
			{
				await _runTask.ConfigureAwait(false);
			}
			cts.Dispose();
		}

		protected abstract Task RunAsync(CancellationToken cancellationToken);
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

	private bool TryGetCoolerState(Guid deviceId, Guid coolerId, [NotNullWhen(true)] out CoolerState? state)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			if (deviceState.CoolerStates is { } coolerStates)
			{
				return coolerStates.TryGetValue(coolerId, out state);
			}
		}
		state = null;
		return false;
	}
}
