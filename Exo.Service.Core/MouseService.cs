using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Configuration;
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
		public PersistedMouseInformation(DotsPerInch maximumDpi, MouseDpiCapabilities capabilities, byte minimumDpiPresetCount, byte maximumDpiPresetCount)
		{
			MaximumDpi = maximumDpi;
			Capabilities = capabilities;
			MinimumDpiPresetCount = minimumDpiPresetCount;
			MaximumDpiPresetCount = maximumDpiPresetCount;
		}

		public DotsPerInch MaximumDpi { get; }
		public MouseDpiCapabilities Capabilities { get; }
		public byte MinimumDpiPresetCount { get; }
		public byte MaximumDpiPresetCount { get; }
	}

	private sealed class DeviceState
	{
		private readonly MouseService _mouseService;
		public Driver? Driver;
		private IMouseDynamicDpiFeature? _dynamicDpiFeature;
		public readonly IConfigurationContainer ConfigurationContainer;
		public readonly object Lock;
		private readonly Action<Driver, MouseDpiStatus> _dpiChangedHandler;
		public ImmutableArray<DotsPerInch> DpiPresets;
		public MouseDpiStatus CurrentDpi;
		public readonly Guid DeviceId;
		public DotsPerInch MaximumDpi;
		public byte MinimumDpiPresetCount;
		public byte MaximumDpiPresetCount;
		public MouseDpiCapabilities Capabilities;

		public DeviceState(MouseService mouseService, IConfigurationContainer configurationContainer, Guid deviceId, PersistedMouseInformation persistedInformation)
		{
			_mouseService = mouseService;
			ConfigurationContainer = configurationContainer;
			Lock = new();
			_dpiChangedHandler = OnDpiChanged;
			DeviceId = deviceId;
			MaximumDpi = persistedInformation.MaximumDpi;
			MinimumDpiPresetCount = persistedInformation.MinimumDpiPresetCount;
			MaximumDpiPresetCount = persistedInformation.MaximumDpiPresetCount;
			Capabilities = persistedInformation.Capabilities;
		}

		public bool Update(PersistedMouseInformation persistedInformation)
		{
			bool isChanged = MaximumDpi != persistedInformation.MaximumDpi;
			MaximumDpi = persistedInformation.MaximumDpi;
			isChanged |= MinimumDpiPresetCount != persistedInformation.MinimumDpiPresetCount;
			MinimumDpiPresetCount = persistedInformation.MinimumDpiPresetCount;
			isChanged |= MaximumDpiPresetCount != persistedInformation.MaximumDpiPresetCount;
			MaximumDpiPresetCount = persistedInformation.MaximumDpiPresetCount;
			isChanged |= Capabilities != persistedInformation.Capabilities;
			Capabilities = persistedInformation.Capabilities;
			return isChanged;
		}

		public PersistedMouseInformation CreatePersistedInformation()
			=> new(MaximumDpi, Capabilities, MinimumDpiPresetCount, MaximumDpiPresetCount);

		public void RegisterNotificationsAndUpdateDpi(IMouseDynamicDpiFeature feature)
		{
			_dynamicDpiFeature = feature;
			lock (Lock)
			{
				feature.DpiChanged += _dpiChangedHandler;
				CurrentDpi = feature.CurrentDpi;
				_mouseService.OnDpiChanged(new(WatchNotificationKind.Addition, DeviceId, CurrentDpi, CurrentDpi, DpiPresets));
			}
		}

		public void UnregisterNotifications()
		{
			if (_dynamicDpiFeature is not null)
			{
				lock (Lock)
				{
					_dynamicDpiFeature.DpiChanged -= _dpiChangedHandler;
				}
				_dynamicDpiFeature = null;
			}
		}

		public MouseDeviceInformation CreateMouseInformation()
			=> new() { DeviceId = DeviceId, IsConnected = Driver is not null, MaximumDpi = MaximumDpi, DpiCapabilities = Capabilities };

		private void OnDpiChanged(Driver driver, MouseDpiStatus dpi)
		{
			// NB: Locking could potentially mess event ordering , so we might want to migrate to channels at some point.
			// Locking could still be necessary for the event registration and initial value update.
			lock (Lock)
			{
				var oldDpi = CurrentDpi;
				CurrentDpi = new MouseDpiStatus() { PresetIndex = dpi.PresetIndex, Dpi = dpi.Dpi };

				if (CurrentDpi != oldDpi)
				{
					_mouseService.OnDpiChanged(new DpiWatchNotification(WatchNotificationKind.Update, DeviceId, CurrentDpi, oldDpi, DpiPresets));
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
	private ChannelWriter<DpiWatchNotification>[]? _dpiChangeListeners;
	private ChannelWriter<MouseDpiPresetsInformation>[]? _dpiPresetChangeListeners;
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
		MouseDpiCapabilities capabilities = 0;
		byte minimumDpiPresetCount = 0;
		byte maximumDpiPresetCount = 0;
		ImmutableArray<DotsPerInch> dpiPresets = [];

		IMouseConfigurableDpiPresetsFeature? configurableDpiPresetsFeature;
		IMouseDpiPresetFeature? dpiPresetFeature;
		IMouseDynamicDpiFeature? dynamicDpiFeature;
		IMouseDpiFeature? dpiFeature;

		if ((configurableDpiPresetsFeature = mouseFeatures.GetFeature<IMouseConfigurableDpiPresetsFeature>()) is not null)
		{
			dpiFeature = dynamicDpiFeature = dpiPresetFeature = configurableDpiPresetsFeature;

			maximumDpi = configurableDpiPresetsFeature.MaximumDpi;
			currentDpi = configurableDpiPresetsFeature.CurrentDpi;
			dpiPresets = configurableDpiPresetsFeature.DpiPresets;
			minimumDpiPresetCount = configurableDpiPresetsFeature.MinPresetCount;
			maximumDpiPresetCount = configurableDpiPresetsFeature.MaxPresetCount;

			capabilities |= MouseDpiCapabilities.ConfigurableDpiPresets | MouseDpiCapabilities.DpiPresets | MouseDpiCapabilities.DynamicDpi;
			if (configurableDpiPresetsFeature.AllowsSeparateXYDpi) capabilities |= MouseDpiCapabilities.SeparateXYDpi;
			if (configurableDpiPresetsFeature.CanChangePreset) capabilities |= MouseDpiCapabilities.DpiPresetChange;
		}
		else if ((dpiPresetFeature = mouseFeatures.GetFeature<IMouseDpiPresetFeature>()) is not null)
		{
			dpiFeature = dynamicDpiFeature = dpiPresetFeature;

			capabilities |= MouseDpiCapabilities.DpiPresets | MouseDpiCapabilities.DynamicDpi;
			if (dpiPresetFeature.CanChangePreset) capabilities |= MouseDpiCapabilities.DpiPresetChange;
			maximumDpi = dpiPresetFeature.MaximumDpi;
			currentDpi = dpiPresetFeature.CurrentDpi;
			dpiPresets = dpiPresetFeature.DpiPresets;
			if (!dpiPresets.IsDefaultOrEmpty) maximumDpiPresetCount = minimumDpiPresetCount = (byte)dpiPresets.Length;
		}
		else if ((dynamicDpiFeature = mouseFeatures.GetFeature<IMouseDynamicDpiFeature>()) is not null)
		{
			dpiFeature = dynamicDpiFeature;

			capabilities |= MouseDpiCapabilities.DynamicDpi;
			maximumDpi = dynamicDpiFeature.MaximumDpi;
			currentDpi = dynamicDpiFeature.CurrentDpi;
		}
		else if ((dpiFeature = mouseFeatures.GetFeature<IMouseDpiFeature>()) is not null)
		{
			currentDpi = dpiFeature.CurrentDpi;
			maximumDpi = currentDpi.Dpi;
		}

		// If any of the information received from the device is incoherent, we entirely disable related features.
		if (dpiPresets.IsDefault ||
			dpiPresets.Length > 255 ||
			(capabilities & (MouseDpiCapabilities.ConfigurableDpiPresets | MouseDpiCapabilities.DpiPresets)) == MouseDpiCapabilities.DpiPresets && dpiPresets.Length == 0)
		{
			minimumDpiPresetCount = 0;
			maximumDpiPresetCount = 0;
			capabilities &= ~(MouseDpiCapabilities.ConfigurableDpiPresets | MouseDpiCapabilities.DpiPresetChange | MouseDpiCapabilities.DpiPresets | MouseDpiCapabilities.SeparateXYDpi);
		}
		else if (minimumDpiPresetCount > maximumDpiPresetCount ||
			(capabilities & MouseDpiCapabilities.ConfigurableDpiPresets) != 0 && (dpiPresets.Length < minimumDpiPresetCount || dpiPresets.Length > maximumDpiPresetCount))
		{
			minimumDpiPresetCount = 0;
			maximumDpiPresetCount = 0;
			if (dpiPresets.Length == 0)
				capabilities = dpiPresets.Length == 0 ?
					capabilities & ~(MouseDpiCapabilities.ConfigurableDpiPresets | MouseDpiCapabilities.SeparateXYDpi) :
					capabilities & ~(MouseDpiCapabilities.ConfigurableDpiPresets | MouseDpiCapabilities.DpiPresetChange | MouseDpiCapabilities.DpiPresets | MouseDpiCapabilities.SeparateXYDpi);
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

		var newInformation = new PersistedMouseInformation(maximumDpi, capabilities, minimumDpiPresetCount, maximumDpiPresetCount);
		bool shouldPersistInformation;
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out deviceState))
		{
			deviceState = new DeviceState(this, _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id), notification.DeviceInformation.Id, newInformation)
			{
				Driver = notification.Driver,
				CurrentDpi = currentDpi,
				DpiPresets = dpiPresets,
			};
			_deviceStates.TryAdd(notification.DeviceInformation.Id, deviceState);
			shouldPersistInformation = true;
		}
		else
		{
			shouldPersistInformation = deviceState.Update(newInformation);
			deviceState.Driver = notification.Driver;
			deviceState.CurrentDpi = currentDpi;
			deviceState.DpiPresets = dpiPresets;
		}

		if (shouldPersistInformation)
		{
			await deviceState.ConfigurationContainer.WriteValueAsync(newInformation, cancellationToken).ConfigureAwait(false);
		}

		_deviceChangeListeners.TryWrite(deviceState.CreateMouseInformation());

		if ((capabilities & MouseDpiCapabilities.DpiPresets) != 0)
		{
			_dpiPresetChangeListeners.TryWrite(new() { DeviceId = notification.DeviceInformation.Id, ActivePresetIndex = currentDpi.PresetIndex, DpiPresets = dpiPresets });
		}

		if ((capabilities & MouseDpiCapabilities.DynamicDpi) != 0)
		{
			deviceState.RegisterNotificationsAndUpdateDpi(dynamicDpiFeature!);
		}
	}

	private void HandleRemoval(DeviceWatchNotification notification)
	{
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState)) return;

		deviceState.UnregisterNotifications();
		deviceState.Driver = null;

		_deviceChangeListeners.TryWrite(deviceState.CreateMouseInformation());
	}

	private void OnDpiChanged(DpiWatchNotification notification)
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

		MouseDeviceInformation[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			for (int i = 0; i < initialNotificationCount; i++)
			{
				yield return initialNotifications[i];
			}
			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceChangeListeners, channel);
		}

		MouseDeviceInformation[] GetInitialNotifications()
		{
			var initialNotifications = new MouseDeviceInformation[_deviceStates.Count];
			int i = 0;
			foreach (var kvp in _deviceStates)
			{
				initialNotifications[i++] = kvp.Value.CreateMouseInformation();
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<DpiWatchNotification> WatchDpiChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<DpiWatchNotification>();

		DpiWatchNotification[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _dpiChangeListeners, channel);
		}

		try
		{
			for (int i = 0; i < initialNotificationCount; i++)
			{
				yield return initialNotifications[i];
			}
			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _dpiChangeListeners, channel);
		}

		DpiWatchNotification[] GetInitialNotifications()
		{
			var initialNotifications = new DpiWatchNotification[_deviceStates.Count];
			int i = 0;
			foreach (var deviceState in _deviceStates.Values)
			{
				MouseDpiStatus currentDpi;
				lock (deviceState.Lock)
				{
					currentDpi = deviceState.CurrentDpi;
				}
				initialNotifications[i++] = new DpiWatchNotification
				(
					WatchNotificationKind.Enumeration,
					deviceState.DeviceId,
					currentDpi,
					currentDpi,
					deviceState.DpiPresets
				);
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<MouseDpiPresetsInformation> WatchDpiPresetAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<MouseDpiPresetsInformation>();

		MouseDpiPresetsInformation[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _dpiPresetChangeListeners, channel);
		}

		try
		{
			for (int i = 0; i < initialNotificationCount; i++)
			{
				yield return initialNotifications[i];
			}
			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _dpiPresetChangeListeners, channel);
		}

		MouseDpiPresetsInformation[] GetInitialNotifications()
		{
			var initialNotifications = new MouseDpiPresetsInformation[_deviceStates.Count];
			int i = 0;
			foreach (var kvp in _deviceStates)
			{
				lock (kvp.Value.Lock)
				{
					initialNotifications[i++] = new() { DeviceId = kvp.Key, ActivePresetIndex = kvp.Value.CurrentDpi.PresetIndex, DpiPresets = kvp.Value.DpiPresets };
				}
			}

			return initialNotifications;
		}
	}
}

internal readonly struct MouseDeviceInformation
{
	public required Guid DeviceId { get; init; }
	public required bool IsConnected { get; init; }
	public required DotsPerInch MaximumDpi { get; init; }
	public required MouseDpiCapabilities DpiCapabilities { get; init; }
}
