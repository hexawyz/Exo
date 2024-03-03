using System.Collections.Concurrent;
using System.Collections.Immutable;
using Exo.Features;
using Exo.Features.MouseFeatures;

namespace Exo.Service.Services;

public sealed class DpiWatcher : Watcher<Guid, MouseDpiStatus, DpiWatchNotification>
{
	private readonly DeviceRegistry _driverRegistry;
	private readonly ConcurrentDictionary<Guid, ImmutableArray<DotsPerInch>> _presets;

	public DpiWatcher(DeviceRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
		_presets = new();
	}

	private ImmutableArray<DotsPerInch> GetPresets(Guid deviceId)
		=> _presets.TryGetValue(deviceId, out var presets) ? presets : [];

	protected override DpiWatchNotification CreateEnumerationResult(Guid key, MouseDpiStatus value)
		=> new(WatchNotificationKind.Enumeration, key, value, default, GetPresets(key));

	protected override DpiWatchNotification CreateAddResult(Guid key, MouseDpiStatus value)
		=> new(WatchNotificationKind.Addition, key, value, default, GetPresets(key));

	protected override DpiWatchNotification CreateRemoveResult(Guid key, MouseDpiStatus value)
		=> new(WatchNotificationKind.Removal, key, default, value, GetPresets(key));

	protected override DpiWatchNotification CreateUpdateResult(Guid key, MouseDpiStatus newValue, MouseDpiStatus oldValue)
		=> new(WatchNotificationKind.Update, key, newValue, oldValue, GetPresets(key));

	protected override async Task WatchAsyncCore(CancellationToken cancellationToken)
	{
		Action<Driver, MouseDpiStatus> onDpiChanged = (driver, dpi) =>
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

					if ((notification.Driver as IDeviceDriver<IMouseDeviceFeature>)?.Features.GetFeature<IMouseDpiPresetFeature>() is { } mouseDpiPresetFeature)
					{
						_presets[deviceId] = mouseDpiPresetFeature.DpiPresets;
						if (Add(deviceId, mouseDpiPresetFeature.CurrentDpi))
						{
							mouseDpiPresetFeature.DpiChanged += onDpiChanged;
						}
					}
					else if ((notification.Driver as IDeviceDriver<IMouseDeviceFeature>)?.Features.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
					{
						if (Add(deviceId, mouseDynamicDpiFeature.CurrentDpi))
						{
							mouseDynamicDpiFeature.DpiChanged += onDpiChanged;
						}
					}
					else if ((notification.Driver as IDeviceDriver<IMouseDeviceFeature>)?.Features.GetFeature<IMouseDpiFeature>() is { } mouseDpiFeature)
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
					if (Remove(notification.DeviceInformation.Id, out _))
					{
						if ((notification.Driver as IDeviceDriver<IMouseDeviceFeature>)?.Features.GetFeature<IMouseDpiPresetFeature>() is { } mouseDpiPresetFeature)
						{
							mouseDpiPresetFeature.DpiChanged -= onDpiChanged;
							_presets.TryRemove(notification.DeviceInformation.Id, out _);
						}
						else if ((notification.Driver as IDeviceDriver<IMouseDeviceFeature>)?.Features.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
						{
							mouseDynamicDpiFeature.DpiChanged -= onDpiChanged;
						}
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
