using Exo.Features;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal sealed class BatteryWatcher : Watcher<Guid, BatteryChangeNotification>
{
	private readonly DriverRegistry _driverRegistry;

	public BatteryWatcher(DriverRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
	}

	protected override async Task WatchAsyncCore(CancellationToken cancellationToken)
	{
		Action<Driver, BatteryState> onBatteryStateChanged = (driver, state) =>
		{
			// The update must be ignored if _currentBatteryLevels does not contain the ID.
			// This avoids having to acquire the lock here.
			if (_driverRegistry.TryGetDeviceId(driver, out var deviceId) && TryGetValue(deviceId, out var oldNotification))
			{
				var notification = new BatteryChangeNotification
				{
					DeviceId = deviceId,
					Level = state.Level,
					BatteryStatus = (Ui.Contracts.BatteryStatus)state.BatteryStatus,
					ExternalPowerStatus = (Ui.Contracts.ExternalPowerStatus)state.ExternalPowerStatus
				};

				TryUpdate(deviceId, notification, oldNotification);
			}
		};

		await foreach (var notification in _driverRegistry.WatchAsync(cancellationToken).ConfigureAwait(false))
		{
			switch (notification.Kind)
			{
			case WatchNotificationKind.Enumeration:
			case WatchNotificationKind.Addition:
				try
				{
					var deviceId = notification.DeviceInformation.Id;

					if (notification.Driver!.Features.GetFeature<IBatteryStateDeviceFeature>() is { } batteryStateFeature)
					{
						var state = batteryStateFeature.BatteryState;
						var batteryNotification = new BatteryChangeNotification
						{
							DeviceId = deviceId,
							Level = state.Level,
							BatteryStatus = (Ui.Contracts.BatteryStatus)state.BatteryStatus,
							ExternalPowerStatus = (Ui.Contracts.ExternalPowerStatus)state.ExternalPowerStatus
						};
						if (Add(deviceId, batteryNotification))
						{
							batteryStateFeature.BatteryStateChanged += onBatteryStateChanged;
						}
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
				break;
			case WatchNotificationKind.Removal:
				try
				{
					if (Remove(notification.DeviceInformation.Id) &&
						notification.Driver!.Features.GetFeature<IBatteryStateDeviceFeature>() is { } batteryStateFeature)
					{
						batteryStateFeature.BatteryStateChanged -= onBatteryStateChanged;
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
				break;
			}
		}
	}
}
