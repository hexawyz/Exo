using Exo.Features.MouseFeatures;

namespace Exo.Service.Services;

public sealed class DpiWatcher : Watcher<Guid, DotsPerInch>
{
	private readonly DeviceRegistry _driverRegistry;

	public DpiWatcher(DeviceRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
	}

	protected override async Task WatchAsyncCore(CancellationToken cancellationToken)
	{
		Action<Driver, DotsPerInch> onDpiChanged = (driver, dpi) =>
		{
			// The update must be ignored if _currentBatteryLevels does not contain the ID.
			// This avoids having to acquire the lock here.
			if (_driverRegistry.TryGetDeviceId(driver, out var deviceId) && TryGetValue(deviceId, out var oldDpi))
			{
				TryUpdate(deviceId, dpi, oldDpi);
			}
		};

		await foreach (var notification in _driverRegistry.WatchAvailableAsync(cancellationToken).ConfigureAwait(false))
		{
			switch (notification.Kind)
			{
			case WatchNotificationKind.Enumeration:
			case WatchNotificationKind.Addition:
				try
				{
					var deviceId = notification.DeviceInformation.Id;

					if (notification.Driver!.Features.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
					{
						if (Add(deviceId, mouseDynamicDpiFeature.CurrentDpi))
						{
							mouseDynamicDpiFeature.DpiChanged += onDpiChanged;
						}
					}
					else if (notification.Driver!.Features.GetFeature<IMouseDpiFeature>() is { } mouseDpiFeature)
					{
						Add(deviceId, mouseDpiFeature.CurrentDpi);
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
					if (Remove(notification.DeviceInformation.Id, out _) &&
						notification.Driver!.Features.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
					{
						mouseDynamicDpiFeature.DpiChanged -= onDpiChanged;
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
