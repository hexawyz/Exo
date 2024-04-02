using Exo.Features;
using Exo.Features.KeyboardFeatures;

namespace Exo.Service;

public sealed class LockedKeysWatcher : Watcher<Guid, LockKeys>
{
	private readonly DeviceRegistry _driverRegistry;

	public LockedKeysWatcher(DeviceRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
	}

	protected override async Task WatchAsyncCore(CancellationToken cancellationToken)
	{
		Action<Driver, LockKeys> onLockedKeysChanged = (driver, state) =>
		{
			if (_driverRegistry.TryGetDeviceId(driver, out var deviceId) && TryGetValue(deviceId, out var oldState))
			{
				TryUpdate(deviceId, state, oldState);
			}
		};

		await foreach (var notification in _driverRegistry.WatchAvailableAsync<IKeyboardDeviceFeature>(cancellationToken).ConfigureAwait(false))
		{
			switch (notification.Kind)
			{
			case WatchNotificationKind.Enumeration:
			case WatchNotificationKind.Addition:
				try
				{
					var deviceId = notification.DeviceInformation.Id;
					var keyboardFeatures = (IDeviceFeatureSet<IKeyboardDeviceFeature>)notification.FeatureSet!;

					if (keyboardFeatures.GetFeature<IKeyboardLockKeysFeature>() is { } lockKeysFeature)
					{
						if (Add(deviceId, lockKeysFeature.LockedKeys))
						{
							lockKeysFeature.LockedKeysChanged += onLockedKeysChanged;
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
						notification.Driver!.GetFeatureSet<IKeyboardDeviceFeature>().GetFeature<IKeyboardLockKeysFeature>() is { } lockKeysFeature)
					{
						lockKeysFeature.LockedKeysChanged -= onLockedKeysChanged;
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
