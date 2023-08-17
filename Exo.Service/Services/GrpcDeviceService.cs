using System.Collections.Concurrent;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Exo.Features;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal class GrpcDeviceService : IDeviceService, IAsyncDisposable
{
	private readonly DriverRegistry _driverRegistry;
	private readonly BatteryWatcher _batteryWatcher;

	public GrpcDeviceService(DriverRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
		_batteryWatcher = new BatteryWatcher(driverRegistry);
	}

	public ValueTask DisposeAsync() => _batteryWatcher.DisposeAsync();

	public async IAsyncEnumerable<WatchNotification<Ui.Contracts.DeviceInformation>> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _driverRegistry.WatchAsync(cancellationToken))
		{
			yield return new()
			{
				NotificationKind = notification.Kind.ToGrpc(),
				Details = notification.DeviceInformation.ToGrpc(),
			};
		}
	}

	public ValueTask<ExtendedDeviceInformation> GetExtendedDeviceInformationAsync(DeviceRequest request, CancellationToken cancellationToken)
	{
		if (!_driverRegistry.TryGetDriver(request.Id, out var driver))
		{
			return ValueTask.FromException<ExtendedDeviceInformation>(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException()));
		}

		string? serialNumber = null;
		DeviceId? deviceId = null;
		if (driver.Features.GetFeature<IDeviceIdDeviceFeature>() is { } deviceIdFeature)
		{
			deviceId = deviceIdFeature.DeviceId.ToGrpc();
		}
		if (driver.Features.GetFeature<ISerialNumberDeviceFeature>() is { } serialNumberFeature)
		{
			serialNumber = serialNumberFeature.SerialNumber;
		}
		bool hasBatteryState = driver.Features.HasFeature<IBatteryStateDeviceFeature>();
		return new(new ExtendedDeviceInformation { DeviceId = deviceId, SerialNumber = serialNumber, HasBatteryState = hasBatteryState });
	}

	public IAsyncEnumerable<BatteryChangeNotification> WatchBatteryChangesAsync(CancellationToken cancellationToken)
		=> _batteryWatcher.WatchAsync(cancellationToken);

	private sealed class BatteryWatcher : IAsyncDisposable
	{
		private readonly ConcurrentDictionary<Guid, BatteryChangeNotification> _currentBatteryStates;
		private ChannelWriter<BatteryChangeNotification>[]? _changeListeners;
		private object? _lock;
		private TaskCompletionSource<CancellationToken> _startRunTaskCompletionSource;
		private CancellationTokenSource? _currentRunCancellationTokenSource;
		private int _watcherCount;
		private readonly CancellationTokenSource _disposeCancellationTokenSource;
		private readonly Task _watchTask;

		public BatteryWatcher(DriverRegistry driverRegistry)
		{
			_currentBatteryStates = new();
			_lock = new();
			_startRunTaskCompletionSource = new();
			_disposeCancellationTokenSource = new();
			_watchTask = WatchDevicesAsync(driverRegistry, _lock, _disposeCancellationTokenSource.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _lock, null) is { } @lock)
			{
				lock (@lock)
				{
					_disposeCancellationTokenSource.Cancel();
					_startRunTaskCompletionSource.TrySetCanceled(_disposeCancellationTokenSource.Token);
					_disposeCancellationTokenSource.Dispose();
				}
				await _watchTask.ConfigureAwait(false);
			}
		}

		private object Lock
		{
			get
			{
				var @lock = Volatile.Read(ref _lock);
				ObjectDisposedException.ThrowIf(@lock is null, typeof(BatteryWatcher));
				return @lock;
			}
		}

		private async Task WatchDevicesAsync(DriverRegistry driverRegistry, object @lock, CancellationToken cancellationToken)
		{
			Action<Driver, BatteryState> onBatteryStateChanged = (driver, state) =>
			{
				// The update must be ignored if _currentBatteryLevels does not contain the ID.
				// This avoids having to acquire the lock here.
				if (driverRegistry.TryGetDeviceId(driver, out var deviceId) && _currentBatteryStates.TryGetValue(deviceId, out var oldNotification))
				{
					var notification = new BatteryChangeNotification
					{
						DeviceId = deviceId,
						Level = state.Level,
						BatteryStatus = (Ui.Contracts.BatteryStatus)state.BatteryStatus, ExternalPowerStatus = (Ui.Contracts.ExternalPowerStatus)state.ExternalPowerStatus
					};

					if (_currentBatteryStates.TryUpdate(deviceId, notification, oldNotification))
					{
						Volatile.Read(ref _changeListeners).TryWrite(notification);
					}
				}
			};

			try
			{
				while (true)
				{
					var currentRunCancellation = await Volatile.Read(ref _startRunTaskCompletionSource).Task.ConfigureAwait(false);

					// This loop can be canceled
					try
					{
						await foreach (var notification in driverRegistry.WatchAsync(cancellationToken).ConfigureAwait(false))
						{
							lock (@lock)
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
											_currentBatteryStates.TryAdd(deviceId, batteryNotification);
											_changeListeners.TryWrite(batteryNotification);
											batteryStateFeature.BatteryStateChanged += onBatteryStateChanged;
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
										if (_currentBatteryStates.TryRemove(notification.DeviceInformation.Id, out _) &&
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
					catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
					{
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private static readonly UnboundedChannelOptions WatchChannelOptions = new() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false };

		public async IAsyncEnumerable<BatteryChangeNotification> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
		{
			ChannelReader<BatteryChangeNotification> reader;

			var channel = Channel.CreateUnbounded<BatteryChangeNotification>(WatchChannelOptions);
			reader = channel.Reader;
			var writer = channel.Writer;

			BatteryChangeNotification[]? currentStates;
			var @lock = Lock;
			lock (@lock)
			{
				currentStates = _currentBatteryStates.Values.ToArray();

				ArrayExtensions.InterlockedAdd(ref _changeListeners, writer);

				if (_watcherCount == 0)
				{
					_currentRunCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token);
					_startRunTaskCompletionSource.TrySetResult(_currentRunCancellationTokenSource.Token);
					_startRunTaskCompletionSource = new();
				}
				_watcherCount++;
			}
			try
			{
				// Publish the initial battery levels.
				foreach (var state in currentStates)
				{
					yield return state;
				}
				currentStates = null;

				await foreach (var state in reader.ReadAllAsync(cancellationToken))
				{
					yield return state;
				}
			}
			finally
			{
				ArrayExtensions.InterlockedRemove(ref _changeListeners, writer);

				lock (@lock)
				{
					if (--_watcherCount == 0)
					{
						_currentRunCancellationTokenSource!.Cancel();
						_currentRunCancellationTokenSource!.Dispose();
						_currentRunCancellationTokenSource = null;
					}
				}
			}
		}
	}
}
