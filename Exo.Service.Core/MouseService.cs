using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Transactions;
using Exo.Configuration;
using Exo.Contracts;
using Exo.Features;
using Exo.Features.Mouses;
using Exo.Programming;
using Exo.Programming.Annotations;
using Exo.Service.Events;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

[Module("Mouse")]
[TypeId(0x397BD522, 0x0E19, 0x4932, 0xBE, 0x80, 0x06, 0xB7, 0x8E, 0x17, 0x2A, 0x64)]
[Event<DeviceEventParameters>("DpiDown", 0xCCCCDEE1, 0x5E77, 0x4DB9, 0x8E, 0x10, 0x3A, 0x82, 0x89, 0x9A, 0xE8, 0x66)]
[Event<DeviceEventParameters>("DpiUp", 0xD40A9183, 0xA9BB, 0x4EDF, 0x93, 0x78, 0x66, 0x90, 0xF2, 0x28, 0x11, 0x9B)]
internal sealed class MouseService
{
	[TypeId(0xBB4A71CB, 0x2894, 0x4388, 0xAE, 0x06, 0x40, 0x06, 0x03, 0x0F, 0x23, 0xBF)]
	private readonly struct PersistedMouseInformation
	{
		[JsonConstructor]
		public PersistedMouseInformation
		(
			DotsPerInch maximumDpi,
			MouseCapabilities capabilities,
			byte minimumDpiPresetCount,
			byte maximumDpiPresetCount,
			ImmutableArray<ushort> supportedPollingFrequencies
		)
		{
			MaximumDpi = maximumDpi;
			Capabilities = capabilities;
			MinimumDpiPresetCount = minimumDpiPresetCount;
			MaximumDpiPresetCount = maximumDpiPresetCount;
			SupportedPollingFrequencies = supportedPollingFrequencies.NotNull();
		}

		public DotsPerInch MaximumDpi { get; }
		public MouseCapabilities Capabilities { get; }
		public byte MinimumDpiPresetCount { get; }
		public byte MaximumDpiPresetCount { get; }
		public ImmutableArray<ushort> SupportedPollingFrequencies { get; }
	}

	private sealed class DeviceState
	{
		private readonly MouseService _mouseService;
		private bool _isConnected;
		private IMouseDynamicDpiFeature? _dynamicDpiFeature;
		private IMouseConfigurablePollingFrequencyFeature? _pollingFrequencyFeature;
		public readonly IConfigurationContainer ConfigurationContainer;
		public readonly object Lock;
		private readonly Action<Driver, MouseDpiStatus> _dpiChangedHandler;
		private ImmutableArray<DotsPerInch> _dpiPresets;
		public ImmutableArray<ushort> SupportedPollingFrequencies;
		private MouseDpiStatus _currentDpi;
		public readonly Guid DeviceId;
		public DotsPerInch MaximumDpi;
		public byte MinimumDpiPresetCount;
		public byte MaximumDpiPresetCount;
		private ushort _currentPollingFrequency;
		private MouseCapabilities _capabilities;

		public DeviceState(MouseService mouseService, IConfigurationContainer configurationContainer, Guid deviceId, PersistedMouseInformation persistedInformation)
		{
			_mouseService = mouseService;
			ConfigurationContainer = configurationContainer;
			Lock = new();
			SupportedPollingFrequencies = [];
			_dpiChangedHandler = OnDpiChanged;
			DeviceId = deviceId;
			MaximumDpi = persistedInformation.MaximumDpi;
			MinimumDpiPresetCount = persistedInformation.MinimumDpiPresetCount;
			MaximumDpiPresetCount = persistedInformation.MaximumDpiPresetCount;
			_capabilities = persistedInformation.Capabilities;
		}

		public bool HasDpi => CurrentDpi != default;
		public bool HasDpiPresets => (Capabilities & (MouseCapabilities.DpiPresets | MouseCapabilities.ConfigurableDpiPresets)) != 0;
		public bool HasPollingFrequency => (Capabilities & (MouseCapabilities.ConfigurablePollingFrequency)) != 0;

		public bool IsConnected => _isConnected;
		public MouseCapabilities Capabilities => _capabilities;
		public ImmutableArray<DotsPerInch> DpiPresets => _dpiPresets;
		public MouseDpiStatus CurrentDpi => _currentDpi;
		public ushort CurrentPollingFrequency => _currentPollingFrequency;

		public bool Update(PersistedMouseInformation persistedInformation)
		{
			bool isChanged = MaximumDpi != persistedInformation.MaximumDpi;
			MaximumDpi = persistedInformation.MaximumDpi;
			isChanged |= MinimumDpiPresetCount != persistedInformation.MinimumDpiPresetCount;
			MinimumDpiPresetCount = persistedInformation.MinimumDpiPresetCount;
			isChanged |= MaximumDpiPresetCount != persistedInformation.MaximumDpiPresetCount;
			MaximumDpiPresetCount = persistedInformation.MaximumDpiPresetCount;
			isChanged |= _capabilities != persistedInformation.Capabilities;
			_capabilities = persistedInformation.Capabilities;
			isChanged |= MinimumDpiPresetCount != persistedInformation.MinimumDpiPresetCount;
			MinimumDpiPresetCount = persistedInformation.MinimumDpiPresetCount;
			isChanged |= MaximumDpiPresetCount != persistedInformation.MaximumDpiPresetCount;
			MaximumDpiPresetCount = persistedInformation.MaximumDpiPresetCount;
			if (!SupportedPollingFrequencies.SequenceEqual(persistedInformation.SupportedPollingFrequencies))
			{
				isChanged = true;
				SupportedPollingFrequencies = persistedInformation.SupportedPollingFrequencies;
			}
			return isChanged;
		}

		public PersistedMouseInformation CreatePersistedInformation()
			=> new(MaximumDpi, Capabilities, MinimumDpiPresetCount, MaximumDpiPresetCount, SupportedPollingFrequencies);

		public void OnConnected(ImmutableArray<DotsPerInch> dpiPresets, MouseDpiStatus currentDpi, ushort currentPollingFrequency, IMouseDynamicDpiFeature? dpiFeature, IMouseConfigurablePollingFrequencyFeature? pollingFrequencyFeature)
		{
			lock (Lock)
			{
				_isConnected = true;
				_dpiPresets = dpiPresets;
				_currentDpi = currentDpi;
				_currentPollingFrequency = currentPollingFrequency;
				if (dpiFeature is not null)
				{
					_dynamicDpiFeature = dpiFeature;
					dpiFeature.DpiChanged += _dpiChangedHandler;
					_currentDpi = dpiFeature.CurrentDpi;
				}
				if (pollingFrequencyFeature is not null)
				{
					_pollingFrequencyFeature = pollingFrequencyFeature;
				}

				// Send all notifications in order.
				// This does not guarantee that they will actually be transmitted, received or processed.
				// However, they are more likely to be processed in order that way.
				// Also, because all notifications are sent within the lock here, any dynamic changes should be processed afterwards.
				// This should greatly diminish the risk of state inconsistencies, which is already low to begin with. (Although I don't think it is entirely avoidable)
				_mouseService.NotifyDeviceConnection(CreateMouseInformation());
				if (_currentDpi != default)
				{
					_mouseService.OnDpiChanged(new(WatchNotificationKind.Addition, DeviceId, CurrentDpi, CurrentDpi, DpiPresets));
				}
				if ((_capabilities & MouseCapabilities.DpiPresets) != 0)
				{
					_mouseService.NotifyDpiPresets(new() { DeviceId = DeviceId, ActivePresetIndex = currentDpi.PresetIndex, DpiPresets = dpiPresets });
				}
				if ((_capabilities & MouseCapabilities.ConfigurablePollingFrequency) != 0)
				{
					_mouseService.NotifyPollingFrequency(new() { DeviceId = DeviceId, PollingFrequency = currentPollingFrequency });
				}
			}
		}

		public void OnDisconnected()
		{
			lock (Lock)
			{
				_isConnected = false;
				if (_dynamicDpiFeature is not null)
				{
					_dynamicDpiFeature.DpiChanged -= _dpiChangedHandler;
				}
				_dynamicDpiFeature = null;
				_pollingFrequencyFeature = null;
			}
		}

		public IMouseConfigurableDpiPresetsFeature? GetConfigurableDpiPresetsFeature()
		{
			lock (Lock)
			{
				if ((Capabilities & MouseCapabilities.ConfigurableDpiPresets) != 0 && _dynamicDpiFeature is IMouseConfigurableDpiPresetsFeature configurableDpiPresetsFeature)
				{
					return configurableDpiPresetsFeature;
				}
			}
			return null;
		}

		public IMouseConfigurablePollingFrequencyFeature? GetConfigurablePollingFrequencyFeature()
		{
			lock (Lock)
			{
				if ((Capabilities & MouseCapabilities.ConfigurablePollingFrequency) != 0 && _pollingFrequencyFeature is { } configurablePollingFrequencyFeature)
				{
					return configurablePollingFrequencyFeature;
				}
			}
			return null;
		}

		public MouseDeviceInformation CreateMouseInformation()
			=> new()
			{
				DeviceId = DeviceId,
				IsConnected = IsConnected,
				MaximumDpi = MaximumDpi,
				DpiCapabilities = Capabilities,
				MinimumDpiPresetCount = MinimumDpiPresetCount,
				MaximumDpiPresetCount = MaximumDpiPresetCount,
				SupportedPollingFrequencies = SupportedPollingFrequencies,
			};

		private void OnDpiChanged(Driver driver, MouseDpiStatus dpi)
		{
			// NB: Locking could potentially mess event ordering , so we might want to migrate to channels at some point.
			// Locking could still be necessary for the event registration and initial value update.
			lock (Lock)
			{
				var oldDpi = CurrentDpi;
				_currentDpi = new MouseDpiStatus() { PresetIndex = dpi.PresetIndex, Dpi = dpi.Dpi };

				if (CurrentDpi != oldDpi)
				{
					_mouseService.OnDpiChanged(new MouseDpiNotification(WatchNotificationKind.Update, DeviceId, CurrentDpi, oldDpi, DpiPresets));
				}
			}
		}
	}

	public static async ValueTask<MouseService> CreateAsync
	(
		ILogger<MouseService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ChannelWriter<Event> eventWriter,
		CancellationToken cancellationToken
	)
	{
		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		return new MouseService
		(
			devicesConfigurationContainer,
			deviceWatcher,
			eventWriter,
			deviceIds.Length > 0 ?
				await ParseDevicesAsync(devicesConfigurationContainer, deviceIds, cancellationToken).ConfigureAwait(false) :
				[]
		);
	}

	private static async Task<List<(Guid DeviceId, IConfigurationContainer DeviceConfigurationContainer, PersistedMouseInformation PersistedInformation)>> ParseDevicesAsync
	(
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		Guid[] deviceIds,
		CancellationToken cancellationToken
	)
	{
		var deviceStates = new List<(Guid DeviceId, IConfigurationContainer DeviceConfigurationContainer, PersistedMouseInformation PersistedInformation)>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			var result = await deviceConfigurationContainer.ReadValueAsync<PersistedMouseInformation>(cancellationToken).ConfigureAwait(false);

			if (!result.Found) continue;

			deviceStates.Add((deviceId, deviceConfigurationContainer, result.Value));
		}

		return deviceStates;
	}

	public static readonly Guid DpiDownEventGuid = new(0xCCCCDEE1, 0x5E77, 0x4DB9, 0x8E, 0x10, 0x3A, 0x82, 0x89, 0x9A, 0xE8, 0x66);
	public static readonly Guid DpiUpEventGuid = new(0xD40A9183, 0xA9BB, 0x4EDF, 0x93, 0x78, 0x66, 0x90, 0xF2, 0x28, 0x11, 0x9B);

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;

	private readonly ChannelWriter<Event> _eventWriter;
	private readonly AsyncLock _lock;
	private readonly object _dpiChangeLock;
	private ChannelWriter<MouseDeviceInformation>[]? _deviceChangeListeners;
	private ChannelWriter<MouseDpiNotification>[]? _dpiChangeListeners;
	private ChannelWriter<MouseDpiPresetsInformation>[]? _dpiPresetChangeListeners;
	private ChannelWriter<MousePollingFrequencyNotification>[]? _pollingFrequencyChangeListeners;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;

	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _watchTask;

	private MouseService
	(
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ChannelWriter<Event> eventWriter,
		IReadOnlyList<(Guid DeviceId, IConfigurationContainer DeviceConfigurationContainer, PersistedMouseInformation PersistedInformation)> deviceStates
	)
	{
		_deviceStates = new();
		if (deviceStates is not null)
		{
			foreach (var (deviceId, configurationContainer, persistedInformation) in deviceStates)
			{
				_deviceStates.TryAdd(deviceId, new(this, configurationContainer, deviceId, persistedInformation));
			}
		}
		_eventWriter = eventWriter;
		_lock = new();
		_dpiChangeLock = new();
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(deviceWatcher, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _watchTask.ConfigureAwait(false);
			cts.Dispose();
		}
	}

	private async Task WatchAsync(IDeviceWatcher deviceWatcher, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in deviceWatcher.WatchAvailableAsync<IMouseDeviceFeature>(cancellationToken))
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
							HandleRemoval(notification);
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
		var mouseFeatures = (IDeviceFeatureSet<IMouseDeviceFeature>)notification.FeatureSet!;
		DotsPerInch maximumDpi = default;
		MouseDpiStatus currentDpi = default;
		ushort currentPollingFrequency = 0;
		MouseCapabilities capabilities = 0;
		byte minimumDpiPresetCount = 0;
		byte maximumDpiPresetCount = 0;
		ImmutableArray<DotsPerInch> dpiPresets = [];
		ImmutableArray<ushort> supportedPollingFrequencies = [];

		IMouseConfigurableDpiPresetsFeature? configurableDpiPresetsFeature;
		IMouseDpiPresetsFeature? dpiPresetFeature;
		IMouseDynamicDpiFeature? dynamicDpiFeature;
		IMouseDpiFeature? dpiFeature;
		IMouseConfigurablePollingFrequencyFeature? configurablePollingFrequencyFeature;

		if ((configurableDpiPresetsFeature = mouseFeatures.GetFeature<IMouseConfigurableDpiPresetsFeature>()) is not null)
		{
			dpiFeature = dynamicDpiFeature = dpiPresetFeature = configurableDpiPresetsFeature;

			maximumDpi = configurableDpiPresetsFeature.MaximumDpi;
			currentDpi = configurableDpiPresetsFeature.CurrentDpi;
			dpiPresets = configurableDpiPresetsFeature.DpiPresets;
			minimumDpiPresetCount = configurableDpiPresetsFeature.MinPresetCount;
			maximumDpiPresetCount = configurableDpiPresetsFeature.MaxPresetCount;

			capabilities |= MouseCapabilities.ConfigurableDpiPresets | MouseCapabilities.DpiPresets | MouseCapabilities.DynamicDpi;
			if (configurableDpiPresetsFeature.AllowsSeparateXYDpi) capabilities |= MouseCapabilities.SeparateXYDpi;
			if (configurableDpiPresetsFeature.CanChangePreset) capabilities |= MouseCapabilities.DpiPresetChange;
		}
		else if ((dpiPresetFeature = mouseFeatures.GetFeature<IMouseDpiPresetsFeature>()) is not null)
		{
			dpiFeature = dynamicDpiFeature = dpiPresetFeature;

			maximumDpi = dpiPresetFeature.MaximumDpi;
			currentDpi = dpiPresetFeature.CurrentDpi;
			dpiPresets = dpiPresetFeature.DpiPresets;
			if (!dpiPresets.IsDefaultOrEmpty) maximumDpiPresetCount = minimumDpiPresetCount = (byte)dpiPresets.Length;

			capabilities |= MouseCapabilities.DpiPresets | MouseCapabilities.DynamicDpi;
			if (dpiPresetFeature.AllowsSeparateXYDpi) capabilities |= MouseCapabilities.SeparateXYDpi;
			if (dpiPresetFeature.CanChangePreset) capabilities |= MouseCapabilities.DpiPresetChange;
		}
		else if ((dynamicDpiFeature = mouseFeatures.GetFeature<IMouseDynamicDpiFeature>()) is not null)
		{
			dpiFeature = dynamicDpiFeature;

			maximumDpi = dynamicDpiFeature.MaximumDpi;
			currentDpi = dynamicDpiFeature.CurrentDpi;

			capabilities |= MouseCapabilities.DynamicDpi;
			if (dynamicDpiFeature.AllowsSeparateXYDpi) capabilities |= MouseCapabilities.SeparateXYDpi;
		}
		else if ((dpiFeature = mouseFeatures.GetFeature<IMouseDpiFeature>()) is not null)
		{
			currentDpi = dpiFeature.CurrentDpi;
			maximumDpi = currentDpi.Dpi;
		}

		if ((configurablePollingFrequencyFeature = mouseFeatures.GetFeature<IMouseConfigurablePollingFrequencyFeature>()) is not null)
		{
			capabilities |= MouseCapabilities.ConfigurablePollingFrequency;
			supportedPollingFrequencies = configurablePollingFrequencyFeature.SupportedPollingFrequencies;
			currentPollingFrequency = configurablePollingFrequencyFeature.PollingFrequency;
		}

		// If any of the information received from the device is incoherent, we entirely disable related features.
		if (dpiPresets.IsDefault ||
			dpiPresets.Length > 255 ||
			(capabilities & (MouseCapabilities.ConfigurableDpiPresets | MouseCapabilities.DpiPresets)) == MouseCapabilities.DpiPresets && dpiPresets.Length == 0)
		{
			minimumDpiPresetCount = 0;
			maximumDpiPresetCount = 0;
			capabilities &= ~(MouseCapabilities.ConfigurableDpiPresets | MouseCapabilities.DpiPresetChange | MouseCapabilities.DpiPresets | MouseCapabilities.SeparateXYDpi);
		}
		else if (minimumDpiPresetCount > maximumDpiPresetCount ||
			(capabilities & MouseCapabilities.ConfigurableDpiPresets) != 0 && (dpiPresets.Length < minimumDpiPresetCount || dpiPresets.Length > maximumDpiPresetCount))
		{
			minimumDpiPresetCount = 0;
			maximumDpiPresetCount = 0;
			if (dpiPresets.Length == 0)
				capabilities = dpiPresets.Length == 0 ?
					capabilities & ~(MouseCapabilities.ConfigurableDpiPresets | MouseCapabilities.SeparateXYDpi) :
					capabilities & ~(MouseCapabilities.ConfigurableDpiPresets | MouseCapabilities.DpiPresetChange | MouseCapabilities.DpiPresets | MouseCapabilities.SeparateXYDpi);
		}

		DeviceState? deviceState;

		// If there are no actual capabilities for the mouse, the device will be removed as a mouse.
		if (capabilities == 0 && maximumDpi == default)
		{
			if (_deviceStates.TryRemove(notification.DeviceInformation.Id, out deviceState))
			{
				await deviceState.ConfigurationContainer.DeleteValueAsync<PersistedMouseInformation>().ConfigureAwait(false);
			}
			return;
		}

		var newInformation = new PersistedMouseInformation(maximumDpi, capabilities, minimumDpiPresetCount, maximumDpiPresetCount, supportedPollingFrequencies);
		bool shouldPersistInformation;
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out deviceState))
		{
			deviceState = new DeviceState(this, _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id), notification.DeviceInformation.Id, newInformation);
			_deviceStates.TryAdd(notification.DeviceInformation.Id, deviceState);
			shouldPersistInformation = true;
		}
		else
		{
			// NB: The lock is not necessary from a logical POV, but it will mostly enforce security and avoid triggering weird bugs from external sources. (GRPC)
			lock (deviceState.Lock)
			{
				shouldPersistInformation = deviceState.Update(newInformation);
			}
		}

		if (shouldPersistInformation)
		{
			await deviceState.ConfigurationContainer.WriteValueAsync(newInformation, cancellationToken).ConfigureAwait(false);
		}

		// This will update the live state of the mouse, setup change handlers, and send out initial notifications.
		deviceState.OnConnected(dpiPresets, currentDpi, currentPollingFrequency, dynamicDpiFeature, configurablePollingFrequencyFeature);
	}

	private void HandleRemoval(DeviceWatchNotification notification)
	{
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState)) return;

		deviceState.OnDisconnected();

		_deviceChangeListeners.TryWrite(deviceState.CreateMouseInformation());
	}

	private void NotifyDeviceConnection(MouseDeviceInformation information) => _deviceChangeListeners.TryWrite(information);
	private void NotifyDpiPresets(MouseDpiPresetsInformation information) => _dpiPresetChangeListeners.TryWrite(information);
	private void NotifyPollingFrequency(MousePollingFrequencyNotification notification) => _pollingFrequencyChangeListeners.TryWrite(notification);

	private void OnDpiChanged(MouseDpiNotification notification)
	{
		lock (_dpiChangeLock)
		{
			_dpiChangeListeners.TryWrite(notification);
			if (notification.NotificationKind == WatchNotificationKind.Update)
			{
				int? status = null;

				if (notification.OldValue.PresetIndex is not null && notification.NewValue.PresetIndex is not null)
				{
					status = Comparer<byte>.Default.Compare(notification.NewValue.PresetIndex.GetValueOrDefault(), notification.OldValue.PresetIndex.GetValueOrDefault());
				}
				else
				{
					int h = Comparer<int>.Default.Compare(notification.NewValue.Dpi.Horizontal, notification.OldValue.Dpi.Horizontal);
					int v = Comparer<int>.Default.Compare(notification.NewValue.Dpi.Vertical, notification.OldValue.Dpi.Vertical);

					if (Math.Sign(h) == Math.Sign(v))
					{
						status = h;
					}
				}

				if (status is not null and not 0)
				{
					_eventWriter.TryWrite
					(
						Event.Create
						(
							status >= 0 ? DpiUpEventGuid : DpiDownEventGuid,
							new MouseDpiEventParameters
							(
								(DeviceId)notification.DeviceId,
								notification.NewValue.Dpi.Horizontal,
								notification.NewValue.Dpi.Vertical,
								checked((byte)notification.Presets.Length),
								notification.NewValue.PresetIndex
							)
						)
					);
				}
			}
		}
	}

	public async IAsyncEnumerable<MouseDeviceInformation> WatchMouseDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<MouseDeviceInformation>();

		List<MouseDeviceInformation>? initialNotifications;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceChangeListeners, channel);
		}

		List<MouseDeviceInformation>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<MouseDeviceInformation>();
			foreach (var kvp in _deviceStates)
			{
				initialNotifications.Add(kvp.Value.CreateMouseInformation());
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<MouseDpiNotification> WatchDpiAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<MouseDpiNotification>();

		List<MouseDpiNotification>? initialNotifications;

		lock (_dpiChangeLock)
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _dpiChangeListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _dpiChangeListeners, channel);
		}

		List<MouseDpiNotification>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<MouseDpiNotification>();
			foreach (var deviceState in _deviceStates.Values)
			{
				MouseDpiStatus currentDpi;
				lock (deviceState.Lock)
				{
					if (!deviceState.IsConnected || !deviceState.HasDpi) continue;

					currentDpi = deviceState.CurrentDpi;
				}
				initialNotifications.Add
				(
					new MouseDpiNotification
					(
						WatchNotificationKind.Enumeration,
						deviceState.DeviceId,
						currentDpi,
						currentDpi,
						deviceState.DpiPresets
					)
				);
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<MouseDpiPresetsInformation> WatchDpiPresetsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<MouseDpiPresetsInformation>();

		List<MouseDpiPresetsInformation>? initialNotifications;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _dpiPresetChangeListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _dpiPresetChangeListeners, channel);
		}

		List<MouseDpiPresetsInformation>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<MouseDpiPresetsInformation>();
			foreach (var kvp in _deviceStates)
			{
				lock (kvp.Value.Lock)
				{
					if (kvp.Value.IsConnected && kvp.Value.HasDpiPresets)
					{
						initialNotifications.Add(new() { DeviceId = kvp.Key, ActivePresetIndex = kvp.Value.CurrentDpi.PresetIndex, DpiPresets = kvp.Value.DpiPresets });
					}
				}
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<MousePollingFrequencyNotification> WatchPollingFrequenciesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<MousePollingFrequencyNotification>();

		List<MousePollingFrequencyNotification>? initialNotifications;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _pollingFrequencyChangeListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _pollingFrequencyChangeListeners, channel);
		}

		List<MousePollingFrequencyNotification>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<MousePollingFrequencyNotification>();
			foreach (var kvp in _deviceStates)
			{
				lock (kvp.Value.Lock)
				{
					if (kvp.Value.IsConnected || kvp.Value.HasPollingFrequency)
					{
						initialNotifications.Add(new() { DeviceId = kvp.Key, PollingFrequency = kvp.Value.CurrentPollingFrequency });
					}
				}
			}

			return initialNotifications;
		}
	}

	public async Task SetDpiPresetsAsync(Guid deviceId, byte activePresetIndex, ImmutableArray<DotsPerInch> presets, CancellationToken cancellationToken)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			if (deviceState.GetConfigurableDpiPresetsFeature() is { } configurableDpiPresetsFeature)
			{
				using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					await configurableDpiPresetsFeature.SetDpiPresetsAsync(activePresetIndex, presets, cancellationToken).ConfigureAwait(false);
					NotifyDpiPresets(new() { DeviceId = deviceId, ActivePresetIndex = activePresetIndex, DpiPresets = presets });
				}
			}
		}
	}

	public async Task SetPollingFrequencyAsync(Guid deviceId, ushort pollingFrequency, CancellationToken cancellationToken)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			if (deviceState.GetConfigurablePollingFrequencyFeature() is { } configurablePollingFrequencyFeature)
			{
				using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					await configurablePollingFrequencyFeature.SetPollingFrequencyAsync(pollingFrequency, cancellationToken).ConfigureAwait(false);
					NotifyPollingFrequency(new() { DeviceId = deviceId, PollingFrequency = pollingFrequency });
				}
			}
		}
	}
}
