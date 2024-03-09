using Exo.Features;
using Exo.Features.KeyboardFeatures;

namespace Exo.Service;

public sealed class BacklightWatcher : Watcher<Guid, BacklightState>
{
	private readonly DeviceRegistry _driverRegistry;

	public BacklightWatcher(DeviceRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
	}

	protected override async Task WatchAsyncCore(CancellationToken cancellationToken)
	{
		Action<Driver, BacklightState> onBacklightStateChanged = (driver, state) =>
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

					if (notification.Driver!.GetFeatures<IKeyboardDeviceFeature>().GetFeature<IKeyboardBacklightFeature>() is { } backlightFeature)
					{
						if (Add(deviceId, backlightFeature.BacklightState))
						{
							backlightFeature.BacklightStateChanged += onBacklightStateChanged;
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
						notification.Driver!.GetFeatures<IKeyboardDeviceFeature>().GetFeature<IKeyboardBacklightFeature>() is { } backlightFeature)
					{
						backlightFeature.BacklightStateChanged -= onBacklightStateChanged;
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
