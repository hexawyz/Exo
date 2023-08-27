using Exo.Overlay.Contracts;

namespace Exo.Service;

public sealed class KeyboardService : IAsyncDisposable
{
	private readonly LockedKeysWatcher _lockedKeysWatcher;
	private readonly BacklightWatcher _backlightWatcher;

	private readonly OverlayNotificationService _overlayNotificationService;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _backlightWatchTask;
	private readonly Task _lockedKeysWatchTask;

	public KeyboardService(LockedKeysWatcher lockedKeysWatcher, BacklightWatcher backlightWatcher, OverlayNotificationService overlayNotificationService)
	{
		_lockedKeysWatcher = lockedKeysWatcher;
		_backlightWatcher = backlightWatcher;
		_overlayNotificationService = overlayNotificationService;
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

	private void NotifyChangedLockKey(in Guid deviceId, LockKeys changedKeys, LockKeys newKeys, LockKeys key, OverlayNotificationKind onNotification, OverlayNotificationKind offNotification)
	{
		if ((changedKeys & key) != 0)
		{
			_overlayNotificationService.PostRequest
			(
				(newKeys & key) != 0 ?
					onNotification :
					offNotification,
				deviceId
			);
		}
	}

	private async Task WatchBacklightAsync(CancellationToken cancellationToken)
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
					_overlayNotificationService.PostRequest
					(
						newLevel < oldLevel ?
							OverlayNotificationKind.KeyboardBacklightDown :
							OverlayNotificationKind.KeyboardBacklightUp,
						notification.Key,
						newLevel,
						notification.NewValue.MaximumLevel
					);
				}
				break;
			}
		}
	}
}
