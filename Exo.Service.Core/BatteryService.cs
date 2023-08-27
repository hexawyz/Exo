using Exo.Features;
using Exo.Overlay.Contracts;

namespace Exo.Service;

public sealed class BatteryService : IAsyncDisposable
{
	private readonly BatteryWatcher _batteryWatcher;

	// NB: We directly send overlay notifications for now.
	// This must be done through the (Not Yet Implemented) event bus later on. (So that notifications can actually be customized)
	// The architecture should be:
	// For discovery: Driver => DriverRegistry => xxxService
	// For notifications: Driver => xxxService => Event Bus => Custom Handler (Where the Custom Handler can actually send the overlay notification)
	// The role of the service layer here is to provide a clean separation from the drivers themselves while providing a general per-feature abstraction.
	// It does induce some overhead but it makes sense as soon as the general feature (e.g. lighting) becomes more complex.
	private readonly OverlayNotificationService _overlayNotificationService;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public BatteryService(BatteryWatcher batteryWatcher, OverlayNotificationService overlayNotificationService)
	{
		_batteryWatcher = batteryWatcher;
		_overlayNotificationService = overlayNotificationService;
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_watchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
	}

	// The notification logic can probably be improved here.
	// The idea is to not flood the user with notifications, but provide useful updates & feedback.
	// One thing that could be added is a general battery status for newly connected devices (not initially enumerated), but it may end up being too verbose ?
	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		await foreach (var notification in _batteryWatcher.WatchAsync(cancellationToken))
		{
			switch (notification.NotificationKind)
			{
			case WatchNotificationKind.Enumeration:
			case WatchNotificationKind.Addition:
				if ((notification.NewValue.ExternalPowerStatus & ExternalPowerStatus.IsConnected) == 0)
				{
					if (notification.NewValue.Level is <= 0.1f)
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryLow, notification.Key, GetBatteryLevel(notification.NewValue.Level.GetValueOrDefault()), 10);
					}
				}
				break;
			case WatchNotificationKind.Update:
				// Detects if the external power status connection state has changed.
				if (((notification.NewValue.ExternalPowerStatus ^ notification.OldValue.ExternalPowerStatus) & ExternalPowerStatus.IsConnected) != 0)
				{
					var notificationKind = (notification.NewValue.ExternalPowerStatus & ExternalPowerStatus.IsConnected) != 0 ?
						OverlayNotificationKind.BatteryExternalPowerConnected :
						OverlayNotificationKind.BatteryExternalPowerDisconnected;
					if (notification.NewValue.Level is not null)
					{
						_overlayNotificationService.PostRequest(notificationKind, notification.Key, GetBatteryLevel(notification.NewValue.Level.GetValueOrDefault()), 10);
					}
					else
					{
						_overlayNotificationService.PostRequest(notificationKind, notification.Key, 0, 0);
					}
				}
				else if (notification.NewValue.BatteryStatus != notification.OldValue.BatteryStatus)
				{
					switch (notification.NewValue.BatteryStatus)
					{
					case BatteryStatus.ChargingComplete:
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryError, notification.Key, 10, 10);
						break;
					case BatteryStatus.Error:
					case BatteryStatus.TooHot:
					case BatteryStatus.Missing:
					case BatteryStatus.Invalid:
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryError, notification.Key);
						break;
					}
				}
				else if (notification.NewValue.BatteryStatus == BatteryStatus.Discharging && notification.NewValue.Level is not null)
				{
					if (notification.NewValue.Level.GetValueOrDefault() <= 0.1f && notification.OldValue.Level is null or > 0.1f)
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryLow, notification.Key, GetBatteryLevel(notification.NewValue.Level.GetValueOrDefault()), 10);
					}
				}
				break;
			}

		}
	}

	private static uint GetBatteryLevel(float level)
	{
		if (level < 0) return 0;
		if (level > 1) return 1;

		return (uint)((level + 0.05f) * 10);
	}

	public IAsyncEnumerable<ChangeWatchNotification<Guid, BatteryState>> WatchChangesAsync(CancellationToken cancellationToken)
		=> _batteryWatcher.WatchAsync(cancellationToken);
}
