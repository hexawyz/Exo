using System.Collections.Concurrent;
using System.Collections.Immutable;
using Exo.Features;
using Exo.Features.MouseFeatures;

namespace Exo.Service;

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

		await foreach (var notification in _driverRegistry.WatchAvailableAsync<IMouseDeviceFeature>(cancellationToken).ConfigureAwait(false))
		{
			switch (notification.Kind)
			{
			case WatchNotificationKind.Enumeration:
			case WatchNotificationKind.Addition:
				try
				{
					var deviceId = notification.DeviceInformation.Id;

					var mouseFeatures = (IDeviceFeatureSet<IMouseDeviceFeature>)notification.FeatureSet!;
					if (mouseFeatures.GetFeature<IMouseDpiPresetFeature>() is { } mouseDpiPresetFeature)
					{
						_presets[deviceId] = mouseDpiPresetFeature.DpiPresets;
						if (Add(deviceId, mouseDpiPresetFeature.CurrentDpi))
						{
							mouseDpiPresetFeature.DpiChanged += onDpiChanged;
						}
					}
					else if (mouseFeatures.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
					{
						if (Add(deviceId, mouseDynamicDpiFeature.CurrentDpi))
						{
							mouseDynamicDpiFeature.DpiChanged += onDpiChanged;
						}
					}
					else if (mouseFeatures.GetFeature<IMouseDpiFeature>() is { } mouseDpiFeature)
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
					if (Remove(notification.DeviceInformation.Id, out _) && notification.Driver is not null)
					{
						// TODO: See if unregistering events is still necessary or not. In its current form, it would not work anymore, as the feature set will always be empty there.
						//var mouseFeatures = notification.Driver!.GetFeatureSet<IMouseDeviceFeature>();
						//if (mouseFeatures.GetFeature<IMouseDpiPresetFeature>() is { } mouseDpiPresetFeature)
						//{
						//	mouseDpiPresetFeature.DpiChanged -= onDpiChanged;
						//	_presets.TryRemove(notification.DeviceInformation.Id, out _);
						//}
						//else if (mouseFeatures.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
						//{
						//	mouseDynamicDpiFeature.DpiChanged -= onDpiChanged;
						//}
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
