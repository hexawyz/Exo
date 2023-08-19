using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Features.MouseFeatures;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

public class GrpcMouseService : IMouseService
{
	private readonly DriverRegistry _driverRegistry;
	private readonly DpiWatcher _dpiWatcher;

	public GrpcMouseService(DriverRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
		_dpiWatcher = new DpiWatcher(driverRegistry);
	}

	//public async IAsyncEnumerable<WatchNotification<MouseDeviceInformation>> WatchMouseDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	//{
	//	await foreach (var notification in _driverRegistry.WatchAsync<IMouseDeviceFeature>(cancellationToken).ConfigureAwait(false))
	//	{
	//		switch (notification.Kind)
	//		{
	//		case WatchNotificationKind.Enumeration:
	//		case WatchNotificationKind.Addition:
	//			yield return new()
	//			{
	//				NotificationKind = notification.Kind.ToGrpc(),
	//				Details = new()
	//				{
	//					DeviceInformation = notification.DeviceInformation.ToGrpc(),
	//					ButtonCount = 3,
	//					HasSeparableDpi = false,
	//					MaximumDpi = new() { Horizontal = 1000, Vertical = 1000 },
	//				},
	//			};
	//			break;
	//		case WatchNotificationKind.Removal:
	//			break;
	//		}
	//	}
	//}

	public IAsyncEnumerable<DpiChangeNotification> WatchDpiChangesAsync(CancellationToken cancellationToken)
		=> _dpiWatcher.WatchAsync(cancellationToken);

	private sealed class DpiWatcher : IAsyncDisposable
	{
		private readonly ConcurrentDictionary<Guid, DpiChangeNotification> _currentDpiValues;
		private ChannelWriter<DpiChangeNotification>[]? _changeListeners;
		private object? _lock;
		private TaskCompletionSource<CancellationToken> _startRunTaskCompletionSource;
		private CancellationTokenSource? _currentRunCancellationTokenSource;
		private int _watcherCount;
		private readonly CancellationTokenSource _disposeCancellationTokenSource;
		private readonly Task _watchTask;

		public DpiWatcher(DriverRegistry driverRegistry)
		{
			_currentDpiValues = new();
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
				ObjectDisposedException.ThrowIf(@lock is null, typeof(DpiWatcher));
				return @lock;
			}
		}

		private async Task WatchDevicesAsync(DriverRegistry driverRegistry, object @lock, CancellationToken cancellationToken)
		{
			Action<Driver, DotsPerInch> onDpiChanged = (driver, dpi) =>
			{
				// The update must be ignored if _currentBatteryLevels does not contain the ID.
				// This avoids having to acquire the lock here.
				if (driverRegistry.TryGetDeviceId(driver, out var deviceId) && _currentDpiValues.TryGetValue(deviceId, out var oldNotification))
				{
					var dpiNotification = new DpiChangeNotification
					{
						DeviceId = deviceId,
						Dpi = new() { Horizontal = dpi.Horizontal, Vertical = dpi.Vertical },
					};

					if (_currentDpiValues.TryUpdate(deviceId, dpiNotification, oldNotification))
					{
						Volatile.Read(ref _changeListeners).TryWrite(dpiNotification);
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

										if (notification.Driver!.Features.GetFeature<IMouseDynamicDpiFeature>() is { } mouseDynamicDpiFeature)
										{
											var dpi = mouseDynamicDpiFeature.CurrentDpi;
											var dpiNotification = new DpiChangeNotification
											{
												DeviceId = deviceId,
												Dpi = new() { Horizontal = dpi.Horizontal, Vertical = dpi.Vertical },
											};
											_currentDpiValues.TryAdd(deviceId, dpiNotification);
											_changeListeners.TryWrite(dpiNotification);
											mouseDynamicDpiFeature.DpiChanged += onDpiChanged;
										}
										else if (notification.Driver!.Features.GetFeature<IMouseDpiFeature>() is { } mouseDpiFeature)
										{
											var dpi = mouseDpiFeature.CurrentDpi;
											var dpiNotification = new DpiChangeNotification
											{
												DeviceId = deviceId,
												Dpi = new() { Horizontal = dpi.Horizontal, Vertical = dpi.Vertical },
											};
											_currentDpiValues.TryAdd(deviceId, dpiNotification);
											_changeListeners.TryWrite(dpiNotification);
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
										if (_currentDpiValues.TryRemove(notification.DeviceInformation.Id, out _) &&
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

		public async IAsyncEnumerable<DpiChangeNotification> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
		{
			ChannelReader<DpiChangeNotification> reader;

			var channel = Channel.CreateUnbounded<DpiChangeNotification>(WatchChannelOptions);
			reader = channel.Reader;
			var writer = channel.Writer;

			DpiChangeNotification[]? currentDpiValues;
			var @lock = Lock;
			lock (@lock)
			{
				currentDpiValues = _currentDpiValues.Values.ToArray();

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
				// Publish the initial DPI values.
				foreach (var state in currentDpiValues)
				{
					yield return state;
				}
				currentDpiValues = null;

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
