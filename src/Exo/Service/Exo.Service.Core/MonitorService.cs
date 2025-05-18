using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Exo.Features;
using Exo.Features.Monitors;
using Exo.Monitors;
using Exo.Primitives;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal class MonitorService : IChangeSource<MonitorInformation>, IChangeSource<MonitorSettingValue>, IAsyncDisposable
{
	private sealed class MonitorDeviceDetails
	{
		public Guid DeviceId { get; }
		public Driver? Driver;
		public ImmutableArray<MonitorSetting> SupportedSettings;
		public readonly Dictionary<MonitorSetting, ContinuousValue> KnownValues;
		public readonly ImmutableArray<NonContinuousValueDescription> InputSources;
		public readonly ImmutableArray<NonContinuousValueDescription> InputLagLevels;
		public readonly ImmutableArray<NonContinuousValueDescription> ResponseTimeLevels;
		public readonly ImmutableArray<NonContinuousValueDescription> OsdLanguages;
		public readonly AsyncLock Lock;

		public MonitorDeviceDetails
		(
			Guid deviceId,
			Driver driver,
			ImmutableArray<MonitorSetting> supportedSettings,
			ImmutableArray<NonContinuousValueDescription> inputSources,
			ImmutableArray<NonContinuousValueDescription> inputLagLevels,
			ImmutableArray<NonContinuousValueDescription> responseTimeLevels,
			ImmutableArray<NonContinuousValueDescription> osdLanguages
		)
		{
			DeviceId = deviceId;
			Driver = driver;
			SupportedSettings = supportedSettings;
			KnownValues = new();
			InputSources = inputSources;
			InputLagLevels = inputLagLevels;
			ResponseTimeLevels = responseTimeLevels;
			OsdLanguages = osdLanguages;
			Lock = new();
			InputLagLevels = inputLagLevels;
			ResponseTimeLevels = responseTimeLevels;
		}
	}

	private readonly Dictionary<Guid, MonitorDeviceDetails> _deviceDetails = new();
	private ChangeBroadcaster<MonitorInformation> _monitorChangeBroadcaster;
	private ChangeBroadcaster<MonitorSettingValue> _settingChangeBroadcaster;
	private readonly Lock _lock = new();
	private CancellationTokenSource? _cancellationTokenSource = new();
	private readonly IDeviceWatcher _deviceWatcher;
	private readonly ILogger<MonitorService> _logger;
	private readonly Task _monitorWatchTask;

	public MonitorService(ILogger<MonitorService> logger, IDeviceWatcher deviceWatcher)
	{
		_logger = logger;
		_deviceWatcher = deviceWatcher;
		_monitorWatchTask = WatchMonitorDevicesAsync(_cancellationTokenSource.Token);
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

	private async Task WatchMonitorDevicesAsync(CancellationToken cancellationToken)
	{
		// NB: This method is the only method that is updating _deviceDetails, so it can read the dictionary without lock, as there will never be a concurrency issue in that case.
		var settingsBuilder = ImmutableArray.CreateBuilder<MonitorSetting>();
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<IMonitorDeviceFeature>(cancellationToken))
			{
				// TODO: For when device features can be updated. 
				if (notification.Kind == WatchNotificationKind.Update) continue;

				var deviceId = notification.DeviceInformation.Id;
				MonitorDeviceDetails? details;

				if (notification.Kind == WatchNotificationKind.Removal)
				{
					try
					{
						// The idea here is to empty out the device details before removing them from the list.
						// So, once the details are guaranteed to be empty once they are no more exposed in the dictionary.
						if (_deviceDetails.TryGetValue(deviceId, out details))
						{
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
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
					}
					catch (Exception ex)
					{
						_logger.MonitorServiceDeviceRemovalError(deviceId, ex);
					}
					continue;
				}
				try
				{
					var monitorFeatures = (IDeviceFeatureSet<IMonitorDeviceFeature>)notification.FeatureSet!;

					ImmutableArray<NonContinuousValueDescription> inputSources = default;
					ImmutableArray<NonContinuousValueDescription> inputLagLevels = default;
					ImmutableArray<NonContinuousValueDescription> responseTimeLevels = default;
					ImmutableArray<NonContinuousValueDescription> osdLanguages = default;
					settingsBuilder.Clear();

					if (monitorFeatures.HasFeature<IMonitorBrightnessFeature>()) settingsBuilder.Add(MonitorSetting.Brightness);
					if (monitorFeatures.HasFeature<IMonitorContrastFeature>()) settingsBuilder.Add(MonitorSetting.Contrast);
					if (monitorFeatures.HasFeature<IMonitorSharpnessFeature>()) settingsBuilder.Add(MonitorSetting.Sharpness);

					if (monitorFeatures.HasFeature<IMonitorSpeakerAudioVolumeFeature>()) settingsBuilder.Add(MonitorSetting.AudioVolume);

					if (monitorFeatures.GetFeature<IMonitorInputSelectFeature>() is { } monitorInputSelectFeature)
					{
						settingsBuilder.Add(MonitorSetting.InputSelect);
						inputSources = monitorInputSelectFeature.AllowedValues;
					}

					if (monitorFeatures.HasFeature<IMonitorRedVideoGainFeature>()) settingsBuilder.Add(MonitorSetting.VideoGainRed);
					if (monitorFeatures.HasFeature<IMonitorGreenVideoGainFeature>()) settingsBuilder.Add(MonitorSetting.VideoGainGreen);
					if (monitorFeatures.HasFeature<IMonitorBlueVideoGainFeature>()) settingsBuilder.Add(MonitorSetting.VideoGainBlue);

					if (monitorFeatures.HasFeature<IMonitorRedVideoBlackLevelFeature>()) settingsBuilder.Add(MonitorSetting.VideoBlackLevelRed);
					if (monitorFeatures.HasFeature<IMonitorGreenVideoBlackLevelFeature>()) settingsBuilder.Add(MonitorSetting.VideoBlackLevelGreen);
					if (monitorFeatures.HasFeature<IMonitorBlueVideoBlackLevelFeature>()) settingsBuilder.Add(MonitorSetting.VideoBlackLevelBlue);

					if (monitorFeatures.HasFeature<IMonitorRedSixAxisSaturationControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisSaturationControlRed);
					if (monitorFeatures.HasFeature<IMonitorYellowSixAxisSaturationControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisSaturationControlYellow);
					if (monitorFeatures.HasFeature<IMonitorGreenSixAxisSaturationControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisSaturationControlGreen);
					if (monitorFeatures.HasFeature<IMonitorCyanSixAxisSaturationControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisSaturationControlCyan);
					if (monitorFeatures.HasFeature<IMonitorBlueSixAxisSaturationControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisSaturationControlBlue);
					if (monitorFeatures.HasFeature<IMonitorMagentaSixAxisSaturationControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisSaturationControlMagenta);

					if (monitorFeatures.HasFeature<IMonitorRedSixAxisHueControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisHueControlRed);
					if (monitorFeatures.HasFeature<IMonitorYellowSixAxisHueControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisHueControlYellow);
					if (monitorFeatures.HasFeature<IMonitorGreenSixAxisHueControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisHueControlGreen);
					if (monitorFeatures.HasFeature<IMonitorCyanSixAxisHueControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisHueControlCyan);
					if (monitorFeatures.HasFeature<IMonitorBlueSixAxisHueControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisHueControlBlue);
					if (monitorFeatures.HasFeature<IMonitorMagentaSixAxisHueControlFeature>()) settingsBuilder.Add(MonitorSetting.SixAxisHueControlMagenta);

					if (monitorFeatures.GetFeature<IMonitorInputLagFeature>() is { } inputLagFeature)
					{
						settingsBuilder.Add(MonitorSetting.InputLag);
						inputLagLevels = inputLagFeature.AllowedValues;
					}

					if (monitorFeatures.GetFeature<IMonitorResponseTimeFeature>() is { } responseTimeFeature)
					{
						settingsBuilder.Add(MonitorSetting.ResponseTime);
						responseTimeLevels = responseTimeFeature.AllowedValues;
					}

					if (monitorFeatures.HasFeature<IMonitorBlueLightFilterLevelFeature>()) settingsBuilder.Add(MonitorSetting.BlueLightFilterLevel);

					if (monitorFeatures.GetFeature<IMonitorOsdLanguageFeature>() is { } osdLanguageFeature)
					{
						settingsBuilder.Add(MonitorSetting.OsdLanguage);
						osdLanguages = osdLanguageFeature.AllowedValues;
					}

					if (monitorFeatures.HasFeature<IMonitorPowerIndicatorToggleFeature>()) settingsBuilder.Add(MonitorSetting.PowerIndicator);

					var settings = settingsBuilder.DrainToImmutable();

					// Create and lock the details to prevent changes to be made before we read all the features.
					details = new MonitorDeviceDetails
					(
						notification.DeviceInformation.Id,
						notification.Driver!,
						settings,
						inputSources,
						inputLagLevels,
						responseTimeLevels,
						osdLanguages
					);

					var deviceLock = await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
					// We want to avoid delaying publishing the device, so we first add a empty state to the dictionary.
					// The requests to fetch VCP codes can take quite some time
					lock (_lock)
					{
						_deviceDetails.Add(deviceId, details);
						var monitorChangeBroadcaster = _monitorChangeBroadcaster.GetSnapshot();
						if (!monitorChangeBroadcaster.IsEmpty)
						{
							monitorChangeBroadcaster.Push(new(deviceId, settings, inputSources, inputLagLevels, responseTimeLevels, osdLanguages));
						}
					}

					// Finish the updates in a separate execution flow. We don't want to slow monitor enumeration because of a single slow device.
					_ = Task.Run(() => PublishChangesAsync(details, deviceLock, cancellationToken), cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
				}
				catch (Exception ex)
				{
					_logger.MonitorServiceDeviceArrivalError(deviceId, ex);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task PublishChangesAsync(MonitorDeviceDetails details, IDisposable deviceLock, CancellationToken cancellationToken)
	{
		try
		{
			await PublishOrRefreshSettingsAsync(details, deviceLock, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.MonitorServiceSettingPublishError(details.DeviceId, ex);
		}
	}

	private async Task RefreshSettingsAsync(MonitorDeviceDetails details, IDisposable deviceLock, CancellationToken cancellationToken)
	{
		try
		{
			await PublishOrRefreshSettingsAsync(details, deviceLock, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.MonitorServiceSettingRefreshError(details.DeviceId, ex);
		}
	}

	private async Task PublishOrRefreshSettingsAsync(MonitorDeviceDetails details, IDisposable deviceLock, CancellationToken cancellationToken)
	{
		using (deviceLock)
		{
			var changes = new List<MonitorSettingValue>();
			var monitorFeatures = details.Driver!.GetFeatureSet<IMonitorDeviceFeature>();

			// Compute all the changes after the details have been published. (Which means new watchers might have both captured the details and registered for update notifications)
			changes.Clear();
			foreach (var setting in details.SupportedSettings)
			{
				try
				{
					// NB: The code below will throw NRE if any of the features has become unavailable (which shouldn't happen, but the whole feature set could have been taken offline)
					// This can be improved later, but it is "fine" for now, as we will catch this exception and do nothing, which is actually the best that must be done in this case.
					ContinuousValue value;
					switch (setting)
					{
					case MonitorSetting.Brightness:
						value = await monitorFeatures.GetFeature<IMonitorBrightnessFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.Contrast:
						value = await monitorFeatures.GetFeature<IMonitorContrastFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.Sharpness:
						value = await monitorFeatures.GetFeature<IMonitorSharpnessFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.AudioVolume:
						value = await monitorFeatures.GetFeature<IMonitorSpeakerAudioVolumeFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.InputSelect:
						value = new ContinuousValue(await monitorFeatures.GetFeature<IMonitorInputSelectFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false), 0, 0);
						break;
					case MonitorSetting.VideoGainRed:
						value = await monitorFeatures.GetFeature<IMonitorRedVideoGainFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.VideoGainGreen:
						value = await monitorFeatures.GetFeature<IMonitorGreenVideoGainFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.VideoGainBlue:
						value = await monitorFeatures.GetFeature<IMonitorBlueVideoGainFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.VideoBlackLevelRed:
						value = await monitorFeatures.GetFeature<IMonitorRedVideoBlackLevelFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.VideoBlackLevelGreen:
						value = await monitorFeatures.GetFeature<IMonitorGreenVideoBlackLevelFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.VideoBlackLevelBlue:
						value = await monitorFeatures.GetFeature<IMonitorBlueVideoBlackLevelFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisSaturationControlRed:
						value = await monitorFeatures.GetFeature<IMonitorRedSixAxisSaturationControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisSaturationControlYellow:
						value = await monitorFeatures.GetFeature<IMonitorYellowSixAxisSaturationControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisSaturationControlGreen:
						value = await monitorFeatures.GetFeature<IMonitorGreenSixAxisSaturationControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisSaturationControlCyan:
						value = await monitorFeatures.GetFeature<IMonitorCyanSixAxisSaturationControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisSaturationControlBlue:
						value = await monitorFeatures.GetFeature<IMonitorBlueSixAxisSaturationControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisSaturationControlMagenta:
						value = await monitorFeatures.GetFeature<IMonitorMagentaSixAxisSaturationControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisHueControlRed:
						value = await monitorFeatures.GetFeature<IMonitorRedSixAxisHueControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisHueControlYellow:
						value = await monitorFeatures.GetFeature<IMonitorYellowSixAxisHueControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisHueControlGreen:
						value = await monitorFeatures.GetFeature<IMonitorGreenSixAxisHueControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisHueControlCyan:
						value = await monitorFeatures.GetFeature<IMonitorCyanSixAxisHueControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisHueControlBlue:
						value = await monitorFeatures.GetFeature<IMonitorBlueSixAxisHueControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.SixAxisHueControlMagenta:
						value = await monitorFeatures.GetFeature<IMonitorMagentaSixAxisHueControlFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.InputLag:
						value = new ContinuousValue(await monitorFeatures.GetFeature<IMonitorInputLagFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false), 0, 0);
						break;
					case MonitorSetting.ResponseTime:
						value = new ContinuousValue(await monitorFeatures.GetFeature<IMonitorResponseTimeFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false), 0, 0);
						break;
					case MonitorSetting.BlueLightFilterLevel:
						value = await monitorFeatures.GetFeature<IMonitorBlueLightFilterLevelFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false);
						break;
					case MonitorSetting.OsdLanguage:
						value = new ContinuousValue(await monitorFeatures.GetFeature<IMonitorOsdLanguageFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false), 0, 0);
						break;
					case MonitorSetting.PowerIndicator:
						value = new ContinuousValue(await monitorFeatures.GetFeature<IMonitorPowerIndicatorToggleFeature>()!.GetValueAsync(cancellationToken).ConfigureAwait(false) ? (ushort)1 : (ushort)0, 0, 1);
						break;
					default:
						continue;
					}

					// Acquire the lock once for each update, and publish the updates immediately.
					// This means that watchers that register will never get duplicate updates.
					lock (_lock)
					{
						if (!details.KnownValues.TryGetValue(setting, out var oldValue))
						{
							details.KnownValues.Add(setting, value);
						}
						else if (oldValue != value)
						{
							details.KnownValues[setting] = value;
						}
						else
						{
							// Skip the change notification if the value was neither created not updated.
							goto UpdateProcessed;
						}
						var settingChangeBroadcaster = _settingChangeBroadcaster.GetSnapshot();
						if (!settingChangeBroadcaster.IsEmpty)
						{
							settingChangeBroadcaster.Push(new(details.DeviceId, setting, value.Current, value.Minimum, value.Maximum));
						}
					UpdateProcessed:;
					}
				}
				catch (Exception ex)
				{
					_logger.MonitorServiceSettingValueRetrievalError(details.DeviceId, setting, ex);
				}
			}
		}
	}

	ValueTask<MonitorInformation[]?> IChangeSource<MonitorInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<MonitorInformation> writer, CancellationToken cancellationToken)
	{
		MonitorInformation[] initialNotifications;
		lock (_lock)
		{
			initialNotifications = new MonitorInformation[_deviceDetails.Count];
			int i = 0;
			foreach (var details in _deviceDetails.Values)
			{
				initialNotifications[i++] = new(details.DeviceId, details.SupportedSettings, details.InputSources, details.InputLagLevels, details.ResponseTimeLevels, details.OsdLanguages);
			}
			_monitorChangeBroadcaster.Register(writer);
		}
		return new(initialNotifications);
	}

	void IChangeSource<MonitorInformation>.UnregisterWatcher(ChannelWriter<MonitorInformation> writer)
		=> _monitorChangeBroadcaster.Unregister(writer);

	ValueTask<MonitorSettingValue[]?> IChangeSource<MonitorSettingValue>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<MonitorSettingValue> writer, CancellationToken cancellationToken)
	{
		List<MonitorSettingValue>? initialNotifications = null;
		lock (_lock)
		{
			foreach (var kvp1 in _deviceDetails)
			{
				foreach (var kvp2 in kvp1.Value.KnownValues)
				{
					(initialNotifications ??= new()).Add(new(kvp1.Key, kvp2.Key, kvp2.Value.Current, kvp2.Value.Minimum, kvp2.Value.Maximum));
				}
			}
			_settingChangeBroadcaster.Register(writer);
		}
		return new(initialNotifications is not null ? [.. initialNotifications] : []);
	}

	void IChangeSource<MonitorSettingValue>.UnregisterWatcher(ChannelWriter<MonitorSettingValue> writer)
		=> _settingChangeBroadcaster.Unregister(writer);

	public ValueTask SetSettingValueAsync(Guid deviceId, MonitorSetting setting, ushort value, CancellationToken cancellationToken)
		=> setting switch
		{
			MonitorSetting.Brightness => SetBrightnessAsync(deviceId, value, cancellationToken),
			MonitorSetting.Contrast => SetContrastAsync(deviceId, value, cancellationToken),
			MonitorSetting.Sharpness => SetSharpnessAsync(deviceId, value, cancellationToken),
			MonitorSetting.AudioVolume => SetAudioVolumeAsync(deviceId, value, cancellationToken),
			MonitorSetting.InputSelect => SetInputSourceAsync(deviceId, value, cancellationToken),
			MonitorSetting.VideoGainRed => SetRedVideoGainAsync(deviceId, value, cancellationToken),
			MonitorSetting.VideoGainGreen => SetGreenVideoGainAsync(deviceId, value, cancellationToken),
			MonitorSetting.VideoGainBlue => SetBlueVideoGainAsync(deviceId, value, cancellationToken),
			MonitorSetting.VideoBlackLevelRed => SetRedVideoBlackLevelAsync(deviceId, value, cancellationToken),
			MonitorSetting.VideoBlackLevelGreen => SetGreenVideoBlackLevelAsync(deviceId, value, cancellationToken),
			MonitorSetting.VideoBlackLevelBlue => SetBlueVideoBlackLevelAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisSaturationControlRed => SetRedSixAxisSaturationControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisSaturationControlYellow => SetYellowSixAxisSaturationControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisSaturationControlGreen => SetGreenSixAxisSaturationControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisSaturationControlCyan => SetCyanSixAxisSaturationControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisSaturationControlBlue => SetBlueSixAxisSaturationControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisSaturationControlMagenta => SetMagentaSixAxisSaturationControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisHueControlRed => SetRedSixAxisHueControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisHueControlYellow => SetYellowSixAxisHueControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisHueControlGreen => SetGreenSixAxisHueControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisHueControlCyan => SetCyanSixAxisHueControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisHueControlBlue => SetBlueSixAxisHueControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.SixAxisHueControlMagenta => SetMagentaSixAxisHueControlAsync(deviceId, value, cancellationToken),
			MonitorSetting.InputLag => SetInputLagAsync(deviceId, value, cancellationToken),
			MonitorSetting.ResponseTime => SetResponseTimeAsync(deviceId, value, cancellationToken),
			MonitorSetting.BlueLightFilterLevel => SetBlueLightFilterLevelAsync(deviceId, value, cancellationToken),
			MonitorSetting.OsdLanguage => SetOsdLanguageAsync(deviceId, value, cancellationToken),
			MonitorSetting.PowerIndicator => SetPowerIndicatorAsync(deviceId, value != 0, cancellationToken),
			_ => ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new UnsupportedSettingException(setting)))
		};



	public async ValueTask RefreshValuesAsync(Guid deviceId, CancellationToken cancellationToken)
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details) || details.Driver is null) goto DeviceNotFound;
		}
		var deviceLock = await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		await RefreshSettingsAsync(details, deviceLock, cancellationToken).ConfigureAwait(false);
		return;
	DeviceNotFound:;
		throw new DeviceNotFoundException();
	}

	private void UpdateCachedSetting(Dictionary<MonitorSetting, ContinuousValue> knownValues, Guid deviceId, MonitorSetting setting, ushort value)
	{
		lock (_lock)
		{
			var settingValue = knownValues[setting] with { Current = value };
			knownValues[setting] = settingValue;
			var settingChangeBroadcaster = _settingChangeBroadcaster.GetSnapshot();
			if (!settingChangeBroadcaster.IsEmpty)
			{
				settingChangeBroadcaster.Push(new(deviceId, setting, settingValue.Current, settingValue.Minimum, settingValue.Maximum));
			}
		}
	}

	public async ValueTask SetValueAsync<TMonitorFeature>(MonitorSetting monitorSetting, Guid deviceId, ushort value, CancellationToken cancellationToken)
		where TMonitorFeature : class, IMonitorDeviceFeature, IContinuousVcpFeature
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details) || details.Driver is null) goto DeviceNotFound;
		}
		using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (details.Driver is null) goto DeviceNotFound;

			if (details.Driver.GetFeatureSet<IMonitorDeviceFeature>().GetFeature<TMonitorFeature>() is not { } feature)
			{
				throw new SettingNotFoundException();
			}

			await feature.SetValueAsync(value, cancellationToken).ConfigureAwait(false);
			UpdateCachedSetting(details.KnownValues, deviceId, monitorSetting, value);
		}
		return;
	DeviceNotFound:;
		throw new DeviceNotFoundException();
	}

	public async ValueTask SetNonContinuousValueAsync<TMonitorFeature>(MonitorSetting monitorSetting, Guid deviceId, ushort value, CancellationToken cancellationToken)
		where TMonitorFeature : class, IMonitorDeviceFeature, INonContinuousVcpFeature
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details) || details.Driver is null) goto DeviceNotFound;
		}
		using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (details.Driver is null) goto DeviceNotFound;

			if (details.Driver.GetFeatureSet<IMonitorDeviceFeature>().GetFeature<TMonitorFeature>() is not { } feature)
			{
				throw new SettingNotFoundException();
			}

			await feature.SetValueAsync(value, cancellationToken).ConfigureAwait(false);
			UpdateCachedSetting(details.KnownValues, deviceId, monitorSetting, value);
		}
		return;
	DeviceNotFound:;
		throw new DeviceNotFoundException();
	}

	public async ValueTask SetValueAsync<TMonitorFeature>(MonitorSetting monitorSetting, Guid deviceId, bool value, CancellationToken cancellationToken)
		where TMonitorFeature : class, IMonitorDeviceFeature, IBooleanVcpFeature
	{
		MonitorDeviceDetails? details;
		lock (_lock)
		{
			if (!_deviceDetails.TryGetValue(deviceId, out details) || details.Driver is null) goto DeviceNotFound;
		}
		using (await details.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (details.Driver is null) goto DeviceNotFound;

			if (details.Driver.GetFeatureSet<IMonitorDeviceFeature>().GetFeature<TMonitorFeature>() is not { } feature)
			{
				throw new SettingNotFoundException();
			}

			await feature.SetValueAsync(value, cancellationToken).ConfigureAwait(false);
			UpdateCachedSetting(details.KnownValues, deviceId, monitorSetting, value ? (ushort)1 : (ushort)0);
		}
		return;
	DeviceNotFound:;
		throw new DeviceNotFoundException();
	}

	public ValueTask SetBrightnessAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorBrightnessFeature>(MonitorSetting.Brightness, deviceId, value, cancellationToken);

	public ValueTask SetContrastAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorContrastFeature>(MonitorSetting.Contrast, deviceId, value, cancellationToken);

	public ValueTask SetSharpnessAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorSharpnessFeature>(MonitorSetting.Sharpness, deviceId, value, cancellationToken);

	public ValueTask SetAudioVolumeAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorSpeakerAudioVolumeFeature>(MonitorSetting.AudioVolume, deviceId, value, cancellationToken);

	public ValueTask SetInputSourceAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetNonContinuousValueAsync<IMonitorInputSelectFeature>(MonitorSetting.InputSelect, deviceId, value, cancellationToken);

	public ValueTask SetRedVideoGainAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorRedVideoGainFeature>(MonitorSetting.VideoGainRed, deviceId, value, cancellationToken);

	public ValueTask SetGreenVideoGainAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorGreenVideoGainFeature>(MonitorSetting.VideoGainGreen, deviceId, value, cancellationToken);

	public ValueTask SetBlueVideoGainAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorBlueVideoGainFeature>(MonitorSetting.VideoGainBlue, deviceId, value, cancellationToken);

	public ValueTask SetRedVideoBlackLevelAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorRedVideoBlackLevelFeature>(MonitorSetting.VideoBlackLevelRed, deviceId, value, cancellationToken);

	public ValueTask SetGreenVideoBlackLevelAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorGreenVideoBlackLevelFeature>(MonitorSetting.VideoBlackLevelGreen, deviceId, value, cancellationToken);

	public ValueTask SetBlueVideoBlackLevelAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorBlueVideoBlackLevelFeature>(MonitorSetting.VideoBlackLevelBlue, deviceId, value, cancellationToken);

	public ValueTask SetRedSixAxisSaturationControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorRedSixAxisSaturationControlFeature>(MonitorSetting.SixAxisSaturationControlRed, deviceId, value, cancellationToken);

	public ValueTask SetYellowSixAxisSaturationControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorYellowSixAxisSaturationControlFeature>(MonitorSetting.SixAxisSaturationControlYellow, deviceId, value, cancellationToken);

	public ValueTask SetGreenSixAxisSaturationControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorGreenSixAxisSaturationControlFeature>(MonitorSetting.SixAxisSaturationControlGreen, deviceId, value, cancellationToken);

	public ValueTask SetCyanSixAxisSaturationControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorCyanSixAxisSaturationControlFeature>(MonitorSetting.SixAxisSaturationControlCyan, deviceId, value, cancellationToken);

	public ValueTask SetBlueSixAxisSaturationControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorBlueSixAxisSaturationControlFeature>(MonitorSetting.SixAxisSaturationControlBlue, deviceId, value, cancellationToken);

	public ValueTask SetMagentaSixAxisSaturationControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorMagentaSixAxisSaturationControlFeature>(MonitorSetting.SixAxisSaturationControlMagenta, deviceId, value, cancellationToken);

	public ValueTask SetRedSixAxisHueControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorRedSixAxisHueControlFeature>(MonitorSetting.SixAxisHueControlRed, deviceId, value, cancellationToken);

	public ValueTask SetYellowSixAxisHueControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorYellowSixAxisHueControlFeature>(MonitorSetting.SixAxisHueControlYellow, deviceId, value, cancellationToken);

	public ValueTask SetGreenSixAxisHueControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorGreenSixAxisHueControlFeature>(MonitorSetting.SixAxisHueControlGreen, deviceId, value, cancellationToken);

	public ValueTask SetCyanSixAxisHueControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorCyanSixAxisHueControlFeature>(MonitorSetting.SixAxisHueControlCyan, deviceId, value, cancellationToken);

	public ValueTask SetBlueSixAxisHueControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorBlueSixAxisHueControlFeature>(MonitorSetting.SixAxisHueControlBlue, deviceId, value, cancellationToken);

	public ValueTask SetMagentaSixAxisHueControlAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorMagentaSixAxisHueControlFeature>(MonitorSetting.SixAxisHueControlMagenta, deviceId, value, cancellationToken);

	public ValueTask SetInputLagAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetNonContinuousValueAsync<IMonitorInputLagFeature>(MonitorSetting.InputLag, deviceId, value, cancellationToken);

	public ValueTask SetResponseTimeAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetNonContinuousValueAsync<IMonitorResponseTimeFeature>(MonitorSetting.ResponseTime, deviceId, value, cancellationToken);

	public ValueTask SetBlueLightFilterLevelAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorBlueLightFilterLevelFeature>(MonitorSetting.BlueLightFilterLevel, deviceId, value, cancellationToken);

	public ValueTask SetOsdLanguageAsync(Guid deviceId, ushort value, CancellationToken cancellationToken)
		=> SetNonContinuousValueAsync<IMonitorOsdLanguageFeature>(MonitorSetting.OsdLanguage, deviceId, value, cancellationToken);

	public ValueTask SetPowerIndicatorAsync(Guid deviceId, bool value, CancellationToken cancellationToken)
		=> SetValueAsync<IMonitorPowerIndicatorToggleFeature>(MonitorSetting.PowerIndicator, deviceId, value, cancellationToken);
}
