using Exo.Overlay.Contracts;

namespace Exo.Service;

public sealed class KeyboardService : IAsyncDisposable
{
	private readonly BacklightWatcher _backlightWatcher;

	private readonly OverlayNotificationService _overlayNotificationService;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _backlightWatchTask;

	public KeyboardService(BacklightWatcher backlightWatcher, OverlayNotificationService overlayNotificationService)
	{
		_backlightWatcher = backlightWatcher;
		_overlayNotificationService = overlayNotificationService;
		_cancellationTokenSource = new();
		_backlightWatchTask = WatchBacklightAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_backlightWatchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _backlightWatchTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
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
