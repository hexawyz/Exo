using System.Threading.Channels;
using Exo.Contracts.Ui.Overlay;
using Exo.Programming;
using Exo.Programming.Annotations;
using Exo.Service.Events;

namespace Exo.Service;

[Module("Keyboard")]
[TypeId(0xF9B4EBCE, 0x82BB, 0x46F4, 0xA4, 0xED, 0x6E, 0x24, 0xA5, 0x4A, 0x04, 0xCB)]
[Event<DeviceEventParameters>("NumLockOn", 0x33FF3815, 0x2F6F, 0x4ACF, 0xBB, 0x78, 0x4E, 0xDC, 0x25, 0x53, 0x6D, 0x3D)]
[Event<DeviceEventParameters>("NumLockOff", 0x098C4B0B, 0x7592, 0x474C, 0x94, 0x4D, 0x94, 0xF3, 0x56, 0x5A, 0x3C, 0x3B)]
[Event<DeviceEventParameters>("CapsLockOn", 0x159230E9, 0xFA80, 0x4897, 0x87, 0xFB, 0x5A, 0xCD, 0x86, 0x6E, 0x22, 0x4E)]
[Event<DeviceEventParameters>("CapsLockOff", 0x69FE69C2, 0x3272, 0x40C9, 0x93, 0x9A, 0xEF, 0x7D, 0xBE, 0xA9, 0xC1, 0x71)]
[Event<DeviceEventParameters>("ScrollLockOn", 0x5F3AFFDA, 0x9976, 0x4DC4, 0x97, 0x0B, 0xBC, 0x6D, 0xFB, 0x5A, 0x4E, 0xD7)]
[Event<DeviceEventParameters>("ScrollLockOff", 0x612E332B, 0xA987, 0x4BF9, 0x82, 0xE2, 0x95, 0x66, 0xF1, 0x36, 0x3E, 0xDD)]
[Event<BacklightLevelEventParameters>("BacklightUp", 0xC6DDEB28, 0xD887, 0x4101, 0x94, 0xCE, 0x39, 0xB1, 0xF8, 0x05, 0xCA, 0xAF)]
[Event<BacklightLevelEventParameters>("BacklightDown", 0x788776DB, 0xB595, 0x4DC9, 0x8B, 0x14, 0x2D, 0x2D, 0xAB, 0x31, 0xFC, 0x79)]
public sealed class KeyboardService : IAsyncDisposable
{
	public static readonly Guid NumLockOnEventGuid = new(0x33FF3815, 0x2F6F, 0x4ACF, 0xBB, 0x78, 0x4E, 0xDC, 0x25, 0x53, 0x6D, 0x3D);
	public static readonly Guid NumLockOffEventGuid = new(0x098C4B0B, 0x7592, 0x474C, 0x94, 0x4D, 0x94, 0xF3, 0x56, 0x5A, 0x3C, 0x3B);

	public static readonly Guid CapsLockOnEventGuid = new(0x159230E9, 0xFA80, 0x4897, 0x87, 0xFB, 0x5A, 0xCD, 0x86, 0x6E, 0x22, 0x4E);
	public static readonly Guid CapsLockOffEventGuid = new(0x69FE69C2, 0x3272, 0x40C9, 0x93, 0x9A, 0xEF, 0x7D, 0xBE, 0xA9, 0xC1, 0x71);

	public static readonly Guid ScrollLockOnEventGuid = new(0x5F3AFFDA, 0x9976, 0x4DC4, 0x97, 0x0B, 0xBC, 0x6D, 0xFB, 0x5A, 0x4E, 0xD7);
	public static readonly Guid ScrollLockOffEventGuid = new(0x612E332B, 0xA987, 0x4BF9, 0x82, 0xE2, 0x95, 0x66, 0xF1, 0x36, 0x3E, 0xDD);

	// These events should have a proper definition with parameters.
	public static readonly Guid BacklightUpEventGuid = new(0xC6DDEB28, 0xD887, 0x4101, 0x94, 0xCE, 0x39, 0xB1, 0xF8, 0x05, 0xCA, 0xAF);
	public static readonly Guid BacklightDownEventGuid = new(0x788776DB, 0xB595, 0x4DC9, 0x8B, 0x14, 0x2D, 0x2D, 0xAB, 0x31, 0xFC, 0x79);

	private readonly LockedKeysWatcher _lockedKeysWatcher;
	private readonly BacklightWatcher _backlightWatcher;

	private readonly ChannelWriter<Event> _eventWriter;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _backlightWatchTask;
	private readonly Task _lockedKeysWatchTask;

	public KeyboardService(LockedKeysWatcher lockedKeysWatcher, BacklightWatcher backlightWatcher, ChannelWriter<Event> eventWriter)
	{
		_lockedKeysWatcher = lockedKeysWatcher;
		_backlightWatcher = backlightWatcher;
		_eventWriter = eventWriter;
		_cancellationTokenSource = new();
		_lockedKeysWatchTask = WatchLockedKeysAsync(_cancellationTokenSource.Token);
		_backlightWatchTask = WatchBacklightAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_lockedKeysWatchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _lockedKeysWatchTask.ConfigureAwait(false);
		await _backlightWatchTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
	}

	private async Task WatchLockedKeysAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _lockedKeysWatcher.WatchAsync(cancellationToken))
			{
				if (notification.NotificationKind == WatchNotificationKind.Update)
				{
					var deviceId = notification.Key;
					var changedKeys = notification.NewValue ^ notification.OldValue;

					if (changedKeys == 0) continue;

					NotifyChangedLockKey(deviceId, changedKeys, notification.NewValue, LockKeys.NumLock, OverlayNotificationKind.NumLockOn, OverlayNotificationKind.NumLockOff);
					NotifyChangedLockKey(deviceId, changedKeys, notification.NewValue, LockKeys.CapsLock, OverlayNotificationKind.CapsLockOn, OverlayNotificationKind.CapsLockOff);
					NotifyChangedLockKey(deviceId, changedKeys, notification.NewValue, LockKeys.ScrollLock, OverlayNotificationKind.ScrollLockOn, OverlayNotificationKind.ScrollLockOff);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private void NotifyChangedLockKey(in Guid deviceId, LockKeys changedKeys, LockKeys newKeys, LockKeys key, OverlayNotificationKind onNotification, OverlayNotificationKind offNotification)
	{
		if ((changedKeys & key) != 0)
		{
			bool isOn = (newKeys & key) != 0;

			_eventWriter.TryWrite
			(
				Event.Create
				(
					key switch
					{
						LockKeys.NumLock => isOn ? NumLockOnEventGuid : NumLockOffEventGuid,
						LockKeys.CapsLock => isOn ? CapsLockOnEventGuid : CapsLockOffEventGuid,
						LockKeys.ScrollLock => isOn ? ScrollLockOnEventGuid : ScrollLockOffEventGuid,
						_ => throw new InvalidOperationException()
					},
					new DeviceEventParameters((DeviceId)deviceId)
				)
			);
		}
	}

	private async Task WatchBacklightAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _backlightWatcher.WatchAsync(cancellationToken))
			{
				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Update:
					byte newLevel = notification.NewValue.CurrentLevel;
					byte oldLevel = notification.OldValue.CurrentLevel;
					if (newLevel != oldLevel)
					{
						_eventWriter.TryWrite
						(
							Event.Create
							(
								newLevel < oldLevel ? BacklightDownEventGuid : BacklightUpEventGuid,
								new BacklightLevelEventParameters((DeviceId)notification.Key, newLevel, notification.NewValue.MaximumLevel)
							)
						);
					}
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}
}
