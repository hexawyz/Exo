using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Features;
using Exo.Features.Lights;
using Exo.Primitives;
using Exo.Programming.Annotations;
using Exo.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

[Module("Light")]
[TypeId(0x91C9424D, 0xB8F0, 0x4318, 0xA5, 0x2C, 0x0F, 0xEC, 0xEA, 0x7D, 0xC4, 0xDE)]
internal sealed partial class LightService : IChangeSource<LightDeviceInformation>, IChangeSource<LightChangeNotification>, IAsyncDisposable
{
	private sealed class DeviceState
	{
		private Driver? _driver;
		public Driver? Driver => _driver;
		public Dictionary<Guid, LightState> Lights { get; }
		private readonly AsyncLock _lock;
		public AsyncLock Lock => _lock;
		private LightService? _service;
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> LampsConfigurationContainer { get; }
		private readonly Guid _id;
		public Guid Id => _id;
		private LightDeviceCapabilities _capabilities;

		public DeviceState
		(
			LightService? service,
			Guid id,
			LightDeviceCapabilities capabilities,
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> lampsConfigurationContainer,
			Dictionary<Guid, LightState> lights
		)
		{
			_service = service;
			_id = id;
			_capabilities = capabilities;
			DeviceConfigurationContainer = deviceConfigurationContainer;
			LampsConfigurationContainer = lampsConfigurationContainer;
			Lights = lights;
			_lock = new();
		}

		public bool IsPolled => (_capabilities & LightDeviceCapabilities.Polled) != 0;

		public bool SetOnline(Driver driver, LightDeviceCapabilities capabilities)
		{
			_driver = driver;
			bool isChanged = false;
			if (capabilities != _capabilities)
			{
				_capabilities = capabilities;
				isChanged = true;
			}
			return isChanged;
		}

		public void SetOffline()
		{
			Volatile.Write(ref _driver, null);
		}

		public async Task RequestRefreshAsync(CancellationToken cancellationToken)
		{
			if (_driver?.GetFeatureSet<ILightDeviceFeature>()?.GetFeature<IPolledLightControllerFeature>() is { } polledLightControllerFeature)
			{
				await polledLightControllerFeature.RequestRefreshAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		// Not ideal but the current code can't have a reference to the service at creation time…
		[MemberNotNull(nameof(_service))]
		public void SetService(LightService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

		public LightDeviceInformation CreateInformation()
		{
			var lights = Lights.Count > 0 ? new LightInformation[Lights.Count] : [];
			int i = 0;
			foreach (var light in Lights.Values)
			{
				lights[i++] = light.CreateInformation();
			}
			return new LightDeviceInformation(_id, _capabilities, ImmutableCollectionsMarshal.AsImmutableArray(lights));
		}

		public PersistedLightDeviceInformation CreatePersistedInformation()
			=> new() { Capabilities = _capabilities };

		public void OnLightChanged(LightChangeNotification notification)
		{
			if (_service is { } service)
			{
				service._lightChangeBroadcaster.Push(notification);
			}
		}
	}

	private sealed class LightState
	{
		private ILight? _light;
		// Manually cache the proper change delegate depending on the current implementation type of the light.
		// Of course, like always, we don't want that stuff to change, but it is better to support it than to have weird runtime bugs later on.
		// This will not avoid all bugs that could be related to the type of a light changing though, 
		private Delegate? _onChanged;
		private readonly DeviceState _device;

		// Metadata
		private readonly Guid _id;
		private LightCapabilities _capabilities;
		private byte _minimumBrightness;
		private byte _maximumBrightness;
		private uint _minimumTemperature;
		private uint _maximumTemperature;

		public LightState(DeviceState device, Guid id)
		{
			_device = device;
			_id = id;
		}

		public LightState
		(
			DeviceState device,
			Guid id,
			LightCapabilities capabilities
,
			byte minimumBrightness,
			byte maximumBrightness,
			uint minimumTemperature,
			uint maximumTemperature
		)
		{
			_device = device;
			_id = id;
			_capabilities = capabilities;
			_minimumBrightness = minimumBrightness;
			_maximumBrightness = maximumBrightness;
			_minimumTemperature = minimumTemperature;
			_maximumTemperature = maximumTemperature;
		}

		public bool SetOnline(ILight light)
		{
			var capabilities = LightCapabilities.None;
			byte minimumBrightness = 0;
			byte maximumBrightness = 0;
			uint minimumTemperature = 0;
			uint maximumTemperature = 0;
			bool isChanged = false;

			if (light is ILightBrightness lightBrightness)
			{
				minimumBrightness = lightBrightness.Minimum;
				maximumBrightness = lightBrightness.Maximum;
				capabilities |= LightCapabilities.Brightness;
				isChanged |= _minimumBrightness != minimumBrightness;
				isChanged |= _maximumBrightness != maximumBrightness;
			}
			if (light is ILightTemperature lightTemperature)
			{
				minimumTemperature = lightTemperature.Minimum;
				maximumTemperature = lightTemperature.Maximum;
				capabilities |= LightCapabilities.Temperature;
				isChanged |= _minimumTemperature != minimumTemperature;
				isChanged |= _maximumTemperature != maximumTemperature;
			}

			isChanged |= _capabilities != capabilities;

			switch (capabilities)
			{
			case LightCapabilities.None:
				if (_onChanged is not LightChangeHandler<Features.Lights.LightState>) _onChanged = new LightChangeHandler<Features.Lights.LightState>(OnLightChanged);
				((ILight<Features.Lights.LightState>)light).Changed += Unsafe.As<LightChangeHandler<Features.Lights.LightState>>(_onChanged);
				break;
			case LightCapabilities.Brightness:
				if (_onChanged is not LightChangeHandler<DimmableLightState>) _onChanged = new LightChangeHandler<DimmableLightState>(OnLightChanged);
				((ILight<DimmableLightState>)light).Changed += Unsafe.As<LightChangeHandler<DimmableLightState>>(_onChanged);
				break;
			case LightCapabilities.Temperature:
				if (_onChanged is not LightChangeHandler<TemperatureAdjustableLightState>) _onChanged = new LightChangeHandler<TemperatureAdjustableLightState>(OnLightChanged);
				((ILight<TemperatureAdjustableLightState>)light).Changed += Unsafe.As<LightChangeHandler<TemperatureAdjustableLightState>>(_onChanged);
				break;
			case LightCapabilities.Brightness | LightCapabilities.Temperature:
				if (_onChanged is not LightChangeHandler<TemperatureAdjustableDimmableLightState>) _onChanged = new LightChangeHandler<TemperatureAdjustableDimmableLightState>(OnLightChanged);
				((ILight<TemperatureAdjustableDimmableLightState>)light).Changed += Unsafe.As<LightChangeHandler<TemperatureAdjustableDimmableLightState>>(_onChanged);
				break;
			default:
				throw new InvalidOperationException("This light type is unsupported at the moment.");
			}

			_light = light;

			if (isChanged)
			{
				_capabilities = capabilities;
				_minimumBrightness = minimumBrightness;
				_maximumBrightness = maximumBrightness;
				_minimumTemperature = minimumTemperature;
				_maximumTemperature = maximumTemperature;

				return true;
			}

			return false;
		}

		public void SetOffline()
		{
			if (Interlocked.Exchange(ref _light, null) is not { } light) return;

			if (_onChanged is not null)
			{
				switch (_capabilities)
				{
				case LightCapabilities.None:
					((ILight<Features.Lights.LightState>)light).Changed += Unsafe.As<LightChangeHandler<Features.Lights.LightState>>(_onChanged);
					break;
				case LightCapabilities.Brightness:
					((ILight<DimmableLightState>)light).Changed += Unsafe.As<LightChangeHandler<DimmableLightState>>(_onChanged);
					break;
				case LightCapabilities.Temperature:
					((ILight<TemperatureAdjustableLightState>)light).Changed += Unsafe.As<LightChangeHandler<TemperatureAdjustableLightState>>(_onChanged);
					break;
				case LightCapabilities.Brightness | LightCapabilities.Temperature:
					((ILight<TemperatureAdjustableDimmableLightState>)light).Changed += Unsafe.As<LightChangeHandler<TemperatureAdjustableDimmableLightState>>(_onChanged);
					break;
				default:
					throw new InvalidOperationException("This light type is unsupported at the moment.");
				}
			}
		}

		public ValueTask SwitchAsync(bool isOn, CancellationToken cancellationToken)
			=> _light is not null ? _light.SwitchAsync(isOn, cancellationToken) : ValueTask.CompletedTask;

		public ValueTask SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
			=> _light is ILightBrightness lightBrightness ? lightBrightness.SetBrightnessAsync(brightness, cancellationToken) : ValueTask.CompletedTask;

		public ValueTask SetTemperatureAsync(uint temperature, CancellationToken cancellationToken)
			=> _light is ILightTemperature lightTemperature ? lightTemperature.SetTemperatureAsync(temperature, cancellationToken) : ValueTask.CompletedTask;

		private void OnLightChanged(Driver driver, Features.Lights.LightState state)
			=> _device.OnLightChanged(CreateNotification(state));

		private void OnLightChanged(Driver driver, DimmableLightState state)
			=> _device.OnLightChanged(CreateNotification(state));

		private void OnLightChanged(Driver driver, TemperatureAdjustableLightState state)
			=> _device.OnLightChanged(CreateNotification(state));

		private void OnLightChanged(Driver driver, TemperatureAdjustableDimmableLightState state)
			=> _device.OnLightChanged(CreateNotification(state));

		public PersistedLightInformation CreatePersistedInformation()
			=> new()
			{
				Capabilities = _capabilities,
				MinimumBrightness = _minimumBrightness,
				MaximumBrightness = _maximumBrightness,
				MinimumTemperature = _minimumTemperature,
				MaximumTemperature = _maximumTemperature,
			};

		public LightInformation CreateInformation()
			=> new
			(
				_id,
				_capabilities,
				_minimumBrightness,
				_maximumBrightness,
				_minimumTemperature,
				_maximumTemperature
			);

		public bool TryCreateChangeNotification(out LightChangeNotification notification)
		{
			if (_light is { } light)
			{
				switch (_capabilities)
				{
				case LightCapabilities.None:
					notification = CreateNotification(Unsafe.As<ILight<Features.Lights.LightState>>(light).CurrentState);
					break;
				case LightCapabilities.Brightness:
					notification = CreateNotification(Unsafe.As<ILight<DimmableLightState>>(light).CurrentState);
					break;
				case LightCapabilities.Temperature:
					notification = CreateNotification(Unsafe.As<ILight<TemperatureAdjustableLightState>>(light).CurrentState);
					break;
				case LightCapabilities.Brightness | LightCapabilities.Temperature:
					notification = CreateNotification(Unsafe.As<ILight<TemperatureAdjustableDimmableLightState>>(light).CurrentState);
					break;
				default:
					goto Fail;
				}
				return true;
			}
		Fail:;
			notification = default;
			return false;
		}

		private LightChangeNotification CreateNotification(Features.Lights.LightState state)
			=> new
			(
				_device.Id,
				_id,
				state.IsOn,
				0,
				0
			);

		private LightChangeNotification CreateNotification(DimmableLightState state)
			=> new
			(
				_device.Id,
				_id,
				state.IsOn,
				state.Brightness,
				0
			);

		private LightChangeNotification CreateNotification(TemperatureAdjustableLightState state)
			=> new
			(
				_device.Id,
				_id,
				state.IsOn,
				0,
				state.Temperature
			);

		private LightChangeNotification CreateNotification(TemperatureAdjustableDimmableLightState state)
			=> new
			(
				_device.Id,
				_id,
				state.IsOn,
				state.Brightness,
				state.Temperature
			);
	}

	private const string LampsConfigurationContainerName = "lmp";

	public static async ValueTask<LightService> CreateAsync
	(
		ILogger<LightService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		CancellationToken cancellationToken
	)
	{
		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			if (deviceConfigurationContainer.TryGetContainer(LampsConfigurationContainerName, GuidNameSerializer.Instance) is not { } lightConfigurationContainer)
			{
				continue;
			}

			LightDeviceCapabilities capabilities = LightDeviceCapabilities.None;
			{
				var result = await deviceConfigurationContainer.ReadValueAsync(SourceGenerationContext.Default.PersistedLightDeviceInformation, cancellationToken).ConfigureAwait(false);
				if (result.Found)
				{
					capabilities = result.Value.Capabilities;
				}
			}

			var lightIds = await lightConfigurationContainer.GetKeysAsync(cancellationToken);

			if (lightIds.Length == 0)
			{
				continue;
			}

			// Because we want light states to reference the device, we create the device state and mutate the dictionary afterwards.
			// Not ideal, but it will work.
			var lights = new Dictionary<Guid, LightState>();
			var deviceState = new DeviceState
			(
				null,
				deviceId,
				capabilities,
				deviceConfigurationContainer,
				lightConfigurationContainer,
				lights
			);

			foreach (var lightId in lightIds)
			{
				PersistedLightInformation info;
				{
					var result = await lightConfigurationContainer.ReadValueAsync(lightId, SourceGenerationContext.Default.PersistedLightInformation, cancellationToken).ConfigureAwait(false);
					if (!result.Found) continue;
					info = result.Value;
				}
				var state = new LightState
				(
					deviceState,
					lightId,
					info.Capabilities,
					info.MinimumBrightness,
					info.MaximumBrightness,
					info.MinimumTemperature,
					info.MaximumTemperature
				);
				lights.Add(lightId, state);
			}

			if (lights.Count > 0)
			{
				deviceStates.TryAdd
				(
					deviceId,
					deviceState
				);
			}
		}

		return new LightService(logger, devicesConfigurationContainer, deviceWatcher, deviceStates);
	}

	private readonly IDeviceWatcher _deviceWatcher;
	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;
	private readonly AsyncLock _lock;
	private ChangeBroadcaster<LightDeviceInformation> _deviceChangeBroadcaster;
	private ChangeBroadcaster<LightChangeNotification> _lightChangeBroadcaster;
	private readonly Timer _pollingTimer;
	private int _polledDeviceCount;
	private readonly int _pollingInterval;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly ILogger<LightService> _logger;

	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _watchTask;

	private LightService
	(
		ILogger<LightService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ConcurrentDictionary<Guid, DeviceState> deviceStates
	)
	{
		_lock = new();
		_logger = logger;
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_deviceWatcher = deviceWatcher;
		_deviceStates = deviceStates;
		foreach (var state in deviceStates.Values)
		{
			state.SetService(this);
		}
		_pollingTimer = new(PollDevices, null, Timeout.Infinite, Timeout.Infinite);
		_pollingInterval = 10_000;
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _watchTask.ConfigureAwait(false);
			await _pollingTimer.DisposeAsync().ConfigureAwait(false);
			cts.Dispose();
		}
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<ILightDeviceFeature>(cancellationToken))
			{
				switch (notification.Kind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					try
					{
						using (await _lock.WaitAsync(cancellationToken))
						{
							await HandleArrivalAsync(notification, cancellationToken).ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						_logger.LightingServiceDeviceArrivalError(notification.DeviceInformation.Id, notification.DeviceInformation.FriendlyName, ex);
					}
					break;
				case WatchNotificationKind.Removal:
					try
					{
						using (await _lock.WaitAsync(cancellationToken))
						{
							await OnDriverRemovedAsync(notification, cancellationToken).ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						_logger.LightingServiceDeviceRemovalError(notification.DeviceInformation.Id, notification.DeviceInformation.FriendlyName, ex);
					}
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async ValueTask HandleArrivalAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		var lightFeatures = (IDeviceFeatureSet<ILightDeviceFeature>)notification.FeatureSet!;

		var lightControllerFeature = lightFeatures.GetFeature<ILightControllerFeature>();
		var lights = lightControllerFeature?.Lights ?? [];
		var capabilities = LightDeviceCapabilities.None;

		var polledLightControllerFeature = lightFeatures.GetFeature<IPolledLightControllerFeature>();
		if (polledLightControllerFeature is not null)
		{
			capabilities |= LightDeviceCapabilities.Polled;
		}

		// The light controller feature is required
		if (lightControllerFeature is null)
		{
			// TODO: Log a warning.
			return;
		}

		var changedLights = new HashSet<Guid>();
		bool isNew = false;

		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			var deviceConfigurationContainer = _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id);
			deviceState = new DeviceState
			(
				this,
				notification.DeviceInformation.Id,
				capabilities,
				deviceConfigurationContainer,
				deviceConfigurationContainer.GetContainer(LampsConfigurationContainerName, GuidNameSerializer.Instance),
				new()
			);
			isNew = true;
		}

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var oldLightId in deviceState.Lights.Keys)
			{
				changedLights.Add(oldLightId);
			}
			foreach (var light in lights)
			{
				changedLights.Remove(light.Id);
			}
			foreach (var deletedLightId in changedLights)
			{
				if (deviceState.Lights.Remove(deletedLightId))
				{
					await deviceState.LampsConfigurationContainer.DeleteValuesAsync(deletedLightId);
				}
			}
			changedLights.Clear();
			foreach (var light in lights)
			{
				if (!deviceState.Lights.TryGetValue(light.Id, out var lightState))
				{
					deviceState.Lights.TryAdd(light.Id, lightState = new(deviceState, light.Id));
				}

				if (lightState.SetOnline(light))
				{
					changedLights.Add(light.Id);
				}
			}

			if (deviceState.SetOnline(notification.Driver!, capabilities) || isNew)
			{
				try
				{
					await _devicesConfigurationContainer.WriteValueAsync(notification.DeviceInformation.Id, deviceState.CreatePersistedInformation(), SourceGenerationContext.Default.PersistedLightDeviceInformation, cancellationToken).ConfigureAwait(false);
				}
				catch
				{
					// TODO: Log
				}
			}

			if (deviceState.IsPolled && Interlocked.Increment(ref _polledDeviceCount) == 1)
			{
				_pollingTimer.Change(_pollingInterval, Timeout.Infinite);
			}

			if (isNew)
			{
				_deviceStates.TryAdd(deviceState.Id, deviceState);
			}

			foreach (var changedLightId in changedLights)
			{
				if (deviceState.Lights.TryGetValue(changedLightId, out var lightState))
				{
					await deviceState.LampsConfigurationContainer.WriteValueAsync(changedLightId, lightState.CreatePersistedInformation(), SourceGenerationContext.Default.PersistedLightInformation, cancellationToken).ConfigureAwait(false);
				}
			}

			var deviceChangeBroadcaster = _deviceChangeBroadcaster.GetSnapshot();
			if (!deviceChangeBroadcaster.IsEmpty)
			{
				deviceChangeBroadcaster.Push(deviceState.CreateInformation());
			}

			var lightChangeBroadcaster = _lightChangeBroadcaster.GetSnapshot();
			if (!lightChangeBroadcaster.IsEmpty)
			{
				foreach (var light in deviceState.Lights.Values)
				{
					if (light.TryCreateChangeNotification(out var changeNotification))
					{
						lightChangeBroadcaster.Push(changeNotification);
					}
				}
			}
		}
	}

	private async ValueTask OnDriverRemovedAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		if (_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (deviceState.IsPolled && Interlocked.Decrement(ref _polledDeviceCount) == 0)
				{
					_pollingTimer.Change(Timeout.Infinite, Timeout.Infinite);
				}
				deviceState.SetOffline();
			}
		}
	}

	private async void PollDevices(object? state)
	{
		if (Volatile.Read(ref _polledDeviceCount) == 0 || Volatile.Read(ref _cancellationTokenSource) is not { } cts) return;

		CancellationToken cancellationToken;
		try
		{
			cancellationToken = cts.Token;
		}
		catch (ObjectDisposedException)
		{
			return;
		}

		try
		{
			List<Task>? tasks = null;
			foreach (var deviceState in _deviceStates.Values)
			{
				using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					if (deviceState.IsPolled)
					{
						(tasks ??= new()).Add(deviceState.RequestRefreshAsync(cancellationToken));
					}
				}
			}
			if (tasks is not null)
			{
				try
				{
					await Task.WhenAll(tasks);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		finally
		{
			if (Volatile.Read(ref _polledDeviceCount) != 0 && Volatile.Read(ref _cancellationTokenSource) is not null)
			{
				_pollingTimer.Change(_pollingInterval, Timeout.Infinite);
			}
		}
	}

	public async ValueTask SwitchLightAsync(Guid deviceId, Guid lightId, bool isOn, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!deviceState.Lights.TryGetValue(lightId, out var lightState)) throw new LightNotFoundException();

			await lightState.SwitchAsync(isOn, cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask SetBrightnessAsync(Guid deviceId, Guid lightId, byte brightness, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!deviceState.Lights.TryGetValue(lightId, out var lightState)) throw new LightNotFoundException();

			await lightState.SetBrightnessAsync(brightness, cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask SetTemperatureAsync(Guid deviceId, Guid lightId, uint temperature, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!deviceState.Lights.TryGetValue(lightId, out var lightState)) throw new LightNotFoundException();

			await lightState.SetTemperatureAsync(temperature, cancellationToken).ConfigureAwait(false);
		}
	}

	async ValueTask<LightDeviceInformation[]?> IChangeSource<LightDeviceInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<LightDeviceInformation> writer, CancellationToken cancellationToken)
	{
		var initialNotifications = new List<LightDeviceInformation>();
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var state in _deviceStates.Values)
			{
				initialNotifications.Add(state.CreateInformation());
			}

			_deviceChangeBroadcaster.Register(writer);
		}
		return [.. initialNotifications];
	}

	void IChangeSource<LightDeviceInformation>.UnsafeUnregisterWatcher(ChannelWriter<LightDeviceInformation> writer)
		=> _deviceChangeBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<LightDeviceInformation>.SafeUnregisterWatcherAsync(ChannelWriter<LightDeviceInformation> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_deviceChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	async ValueTask<LightChangeNotification[]?> IChangeSource<LightChangeNotification>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<LightChangeNotification> writer, CancellationToken cancellationToken)
	{
		var initialNotifications = new List<LightChangeNotification>();
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var device in _deviceStates.Values)
			{
				using (await device.Lock.WaitAsync(cancellationToken))
				{
					foreach (var light in device.Lights.Values)
					{
						if (light.TryCreateChangeNotification(out var notification))
						{
							initialNotifications.Add(notification);
						}
					}
				}
			}

			_lightChangeBroadcaster.Register(writer);
		}
		return [.. initialNotifications];
	}

	void IChangeSource<LightChangeNotification>.UnsafeUnregisterWatcher(ChannelWriter<LightChangeNotification> writer)
		=> _lightChangeBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<LightChangeNotification>.SafeUnregisterWatcherAsync(ChannelWriter<LightChangeNotification> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_lightChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}
}
