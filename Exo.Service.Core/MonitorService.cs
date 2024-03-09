using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Features;
using Exo.Features.MonitorFeatures;

namespace Exo.Service;

internal class MonitorService : IAsyncDisposable
{
	private sealed class MonitorDeviceDetails
	{
		public Driver? Driver;
		public MonitorSetting[] SupportedSettings;
		public readonly Dictionary<MonitorSetting, ContinuousValue> KnownValues;
		public readonly AsyncLock Lock;

		public MonitorDeviceDetails(Driver driver, MonitorSetting[] supportedSettings)
		{
			Driver = driver;
			SupportedSettings = supportedSettings;
			KnownValues = new();
			Lock = new();
		}
	}

	private readonly Dictionary<Guid, MonitorDeviceDetails> _deviceDetails = new();
	private ChannelWriter<MonitorSettingWatchNotification>[]? _changeListeners = [];
	private readonly object _lock = new();
	private CancellationTokenSource? _cancellationTokenSource = new();
	private readonly IDeviceWatcher _deviceWatcher;
	private readonly Task _monitorWatchTask;

	public MonitorService(IDeviceWatcher deviceWatcher)
	{
		_deviceWatcher = deviceWatcher;
		_monitorWatchTask = WatchMonitorsAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _monitorWatchTask.ConfigureAwait(false);
			cts.Dispose();
		}
	}

	private async Task WatchMonitorsAsync(CancellationToken cancellationToken)
	{
		// NB: This method is the only method that is updating _deviceDetails, so it can read the dictionary without lock, as there will never be a concurrency issue in that case.
		try
		{
			var settings = new List<MonitorSetting>();

			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<IMonitorDeviceFeature>(cancellationToken))
			{
				try
				{
					// TODO: For when device features can be updated. 
					if (notification.Kind == WatchNotificationKind.Update) continue;

					var deviceId = notification.DeviceInformation.Id;

					MonitorDeviceDetails? details;

					if (notification.Kind == WatchNotificationKind.Removal)
					{
						// The idea here is to empty out the device details before removing them from the list.
						// So, once the details are guaranteed to be empty once they are no more exposed in the dictionary.
						if (!_deviceDetails.TryGetValue(deviceId, out details)) continue;
						using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							details.KnownValues.Clear();
							details.Driver = null;
							lock (_lock)
							{
								_deviceDetails.Remove(deviceId, details);
							}
						}
					}

					var monitorFeatures = notification.Driver!.GetFeatures<IMonitorDeviceFeature>();

					settings.Clear();

					if (monitorFeatures.HasFeature<IMonitorBrightnessFeature>())
					{
						settings.Add(MonitorSetting.Brightness);
					}
					if (monitorFeatures.HasFeature<IMonitorContrastFeature>())
					{
						settings.Add(MonitorSetting.Contrast);
					}
					if (monitorFeatures.HasFeature<IMonitorSpeakerAudioVolumeFeature>())
					{
						settings.Add(MonitorSetting.AudioVolume);
					}

					// Create and lock the details to prevent changes to be made before we read all the features.
					details = new MonitorDeviceDetails(notification.Driver!, [.. settings]);

					var deviceLock = await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
					// We want to avoid delaying publishing the device, so we first add a empty state to the dictionary.
					// The requests to fetch VCP codes can take quite some time
					lock (_lock)
					{
						_deviceDetails.Add(deviceId, details);
					}

					// Finish the updates in a separate execution flow. We don't want to slow monitor enumeration because of a single slow device.
					_ = Task.Run(() => PublishChangesAsync(deviceId, details, deviceLock, cancellationToken));
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task PublishChangesAsync(Guid deviceId, MonitorDeviceDetails details, IDisposable deviceLock, CancellationToken cancellationToken)
	{
		var changes = new List<MonitorSettingWatchNotification>();
		try
		{
			using (deviceLock)
			{
				var monitorFeatures = details.Driver!.GetFeatures<IMonitorDeviceFeature>();

				// Compute all the changes after the details have been published. (Which means new watchers might have both captured the details and registered for update notifications)
				changes.Clear();
				foreach (var setting in details.SupportedSettings)
				{
					// NB: The code below will throw NRE if any of the features has become unavailable (which shouldn't happen, but the whole feature set could have been taken offline)
					// This can be improved later, but it is "fine" for now, as we will catch this exception and do nothing, which is actually the best that must be done in this case.
					ContinuousValue value;
					switch (setting)
					{
					case MonitorSetting.Brightness:
						value = await monitorFeatures.GetFeature<IMonitorBrightnessFeature>()!.GetBrightnessAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.Contrast:
						value = await monitorFeatures.GetFeature<IMonitorContrastFeature>()!.GetContrastAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.AudioVolume:
						value = await monitorFeatures.GetFeature<IMonitorSpeakerAudioVolumeFeature>()!.GetVolumeAsync(cancellationToken).ConfigureAwait(false);
						break;
					default:
						continue;
					}

					// Acquire the lock once for each update, and publish the updates immediately.
					// This means that watchers that register will never get duplicate updates.
					lock (_lock)
					{
						details.KnownValues.Add(setting, value);
						_changeListeners.TryWrite(new(deviceId, setting, value.Current, value.Minimum, value.Maximum));
					}
				}
			}
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	public async IAsyncEnumerable<MonitorSettingWatchNotification> WatchSettingsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<MonitorSettingWatchNotification>();

		// Cache all the initial notifications and register the channel for notifications.
		// Registering a listener here could inflict a little perf hit on the MonitorService if there are lots of monitors and settings, but this intended for use by the settings UI only.
		// As such, it should never really be a bottleneck.
		var initialNotifications = new List<MonitorSettingWatchNotification>();
		lock (_lock)
		{
			foreach (var kvp1 in _deviceDetails)
			{
				foreach (var kvp2 in kvp1.Value.KnownValues)
				{
					initialNotifications.Add(new(kvp1.Key, kvp2.Key, kvp2.Value.Current, kvp2.Value.Minimum, kvp2.Value.Maximum));
				}
			}
			ArrayExtensions.InterlockedAdd(ref _changeListeners, channel);
		}

		try
		{
			foreach (var notification in initialNotifications)
			{
				yield return notification;
			}

			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _changeListeners, channel);
		}
	}

	public async ValueTask<ImmutableArray<MonitorSetting>> GetSupportedSettingsAsync(Guid deviceId, CancellationToken cancellationToken)
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details)) return [];
		}
		using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			return ImmutableCollectionsMarshal.AsImmutableArray(details.SupportedSettings);
		}
	}

	public ValueTask SetSettingValueAsync(Guid deviceId, MonitorSetting setting, ushort value, CancellationToken cancellationToken)
		=> setting switch
		{
			MonitorSetting.Brightness => SetBrightnessAsync(deviceId, value, cancellationToken),
			MonitorSetting.Contrast => SetContrastAsync(deviceId, value, cancellationToken),
			MonitorSetting.AudioVolume => SetAudioVolumeAsync(deviceId, value, cancellationToken),
			_ => ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException($"Unsupported setting: {setting}.")))
		};

	private void UpdateCachedSetting(Dictionary<MonitorSetting, ContinuousValue> knownValues, Guid deviceId, MonitorSetting setting, ushort value)
	{
		lock (_lock)
		{
			var settingValue = knownValues[setting] with { Current = value };
			knownValues[setting] = settingValue;
			_changeListeners.TryWrite(new(deviceId, setting, settingValue.Current, settingValue.Minimum, settingValue.Maximum));
		}
	}

	public async ValueTask SetBrightnessAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details) || details.Driver is null) goto DeviceNotFound;
		}
		using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (details.Driver is null) goto DeviceNotFound;

			if (details.Driver.GetFeatures<IMonitorDeviceFeature>().GetFeature<IMonitorBrightnessFeature>() is not { } feature)
			{
				throw new InvalidOperationException("The requested feature is not supported.");
			}

			await feature.SetBrightnessAsync(value, cancellationToken).ConfigureAwait(false);
			UpdateCachedSetting(details.KnownValues, deviceId, MonitorSetting.Brightness, value);
		}
		return;
	DeviceNotFound:;
		throw new InvalidOperationException("Device was not found.");
	}

	public async ValueTask SetContrastAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details) || details.Driver is null) goto DeviceNotFound;
		}
		using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (details.Driver is null) goto DeviceNotFound;

			if (details.Driver.GetFeatures<IMonitorDeviceFeature>().GetFeature<IMonitorContrastFeature>() is not { } feature)
			{
				throw new InvalidOperationException("The requested feature is not supported.");
			}

			await feature.SetContrastAsync(value, cancellationToken).ConfigureAwait(false);
			UpdateCachedSetting(details.KnownValues, deviceId, MonitorSetting.Contrast, value);
		}
		return;
	DeviceNotFound:;
		throw new InvalidOperationException("Device was not found.");
	}

	public async ValueTask SetAudioVolumeAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details) || details.Driver is null) goto DeviceNotFound;
		}
		using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (details.Driver is null) goto DeviceNotFound;

			if (details.Driver.GetFeatures<IMonitorDeviceFeature>().GetFeature<IMonitorSpeakerAudioVolumeFeature>() is not { } feature)
			{
				throw new InvalidOperationException("The requested feature is not supported.");
			}

			await feature.SetVolumeAsync(value, cancellationToken).ConfigureAwait(false);
			UpdateCachedSetting(details.KnownValues, deviceId, MonitorSetting.AudioVolume, value);
		}
		return;
	DeviceNotFound:;
		throw new InvalidOperationException("Device was not found.");
	}
}
