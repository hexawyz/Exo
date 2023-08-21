using Exo.Features.MouseFeatures;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal sealed class DpiWatcher : Watcher<Guid, DpiChangeNotification>
{
	private readonly DriverRegistry _driverRegistry;

	public DpiWatcher(DriverRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
	}

	protected override async Task WatchAsyncCore(CancellationToken cancellationToken)
	{
		Action<Driver, DotsPerInch> onDpiChanged = (driver, dpi) =>
		{
			// The update must be ignored if _currentBatteryLevels does not contain the ID.
			// This avoids having to acquire the lock here.
			if (_driverRegistry.TryGetDeviceId(driver, out var deviceId) && TryGetValue(deviceId, out var oldNotification))
			{
				var dpiNotification = new DpiChangeNotification
				{
					DeviceId = deviceId,
					Dpi = new() { Horizontal = dpi.Horizontal, Vertical = dpi.Vertical },
				};

				TryUpdate(deviceId, dpiNotification, oldNotification);
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

					if (notification.Driver!.Features.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
					{
						var dpi = mouseDynamicDpiFeature.CurrentDpi;
						var dpiNotification = new DpiChangeNotification
						{
							DeviceId = deviceId,
							Dpi = new() { Horizontal = dpi.Horizontal, Vertical = dpi.Vertical },
						};
						if (Add(deviceId, dpiNotification))
						{
							mouseDynamicDpiFeature.DpiChanged += onDpiChanged;
						}
					}
					else if (notification.Driver!.Features.GetFeature<IMouseDpiFeature>() is { } mouseDpiFeature)
					{
						var dpi = mouseDpiFeature.CurrentDpi;
						var dpiNotification = new DpiChangeNotification
						{
							DeviceId = deviceId,
							Dpi = new() { Horizontal = dpi.Horizontal, Vertical = dpi.Vertical },
						};
						Add(deviceId, dpiNotification);
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
