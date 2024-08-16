using Exo.Features;
using Exo.Features.PowerManagement;

namespace Exo.Service;

public sealed class BatteryWatcher : Watcher<Guid, BatteryState>
{
	private readonly DeviceRegistry _driverRegistry;

	public BatteryWatcher(DeviceRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
	}

	protected override async Task WatchAsyncCore(CancellationToken cancellationToken)
	{
		Action<Driver, BatteryState> onBatteryStateChanged = (driver, state) =>
		{
			// The update must be ignored if _currentBatteryLevels does not contain the ID.
			// This avoids having to acquire the lock here.
			if (_driverRegistry.TryGetDeviceId(driver, out var deviceId) && TryGetValue(deviceId, out var oldState))
			{
				TryUpdate(deviceId, state, oldState);
			}
		};

		await foreach (var notification in _driverRegistry.WatchAvailableAsync<IPowerManagementDeviceFeature>(cancellationToken).ConfigureAwait(false))
		{
			switch (notification.Kind)
			{
			case WatchNotificationKind.Enumeration:
			case WatchNotificationKind.Addition:
				try
				{
					var deviceId = notification.DeviceInformation.Id;

					if (notification.Driver!.GetFeatureSet<IPowerManagementDeviceFeature>().GetFeature<IBatteryStateDeviceFeature>() is { } batteryStateFeature)
					{
						if (Add(deviceId, batteryStateFeature.BatteryState))
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
					if (Remove(notification.DeviceInformation.Id, out var v) &&
						notification.Driver!.GetFeatureSet<IPowerManagementDeviceFeature>().GetFeature<IBatteryStateDeviceFeature>() is { } batteryStateFeature)
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
