using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

public sealed partial class SensorService
{
	private sealed class PollingSchedulerDisabledException : Exception
	{
		public PollingSchedulerDisabledException() : base("The scheduler is disabled.") { }
	}

	// The scheduler will tick at the requested rate when enabled.
	// It is used as a synchronization source for all polled sensors.
	private sealed class PollingScheduler : IDisposable
	{
		private static readonly TaskCompletionSource NonAcquiredTickSignal = ThrowIfNotAcquired();

		private static TaskCompletionSource ThrowIfNotAcquired()
		{
			var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			tcs.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new PollingSchedulerDisabledException()));
			return tcs;
		}

		private TaskCompletionSource _tickSignal;
		private readonly object _lock;
		private int _referenceCount;
		private readonly int _period;
		private Timer? _timer;
		private readonly ILogger<PollingScheduler> _logger;

		public PollingScheduler(ILogger<PollingScheduler> logger, int period)
		{
			_logger = logger;
			_lock = new();
			_tickSignal = NonAcquiredTickSignal;
			_period = period;
			_timer = new Timer(OnTick, null, Timeout.Infinite, period);
		}

		private void OnTick(object? state)
		{
			bool lockTaken = false;
			try
			{
				Monitor.TryEnter(_lock, ref lockTaken);
				if (!lockTaken) return;
				if (_referenceCount == 0) return;
				_tickSignal.TrySetResult();
				Volatile.Write(ref _tickSignal, new(TaskCreationOptions.RunContinuationsAsynchronously));
			}
			finally
			{
				if (lockTaken)
				{
					Monitor.Exit(_lock);
				}
			}
		}

		public void Acquire()
		{
			lock (_lock)
			{
				ObjectDisposedException.ThrowIf(_timer is null, typeof(PollingScheduler));
				_logger.SensorServicePollingSchedulerAcquire(_referenceCount);
				if (_referenceCount == 0)
				{
					Volatile.Write(ref _tickSignal, new(TaskCreationOptions.RunContinuationsAsynchronously));
					_timer.Change(0, _period);
					_logger.SensorServicePollingSchedulerEnabled();
				}
				_referenceCount++;
			}
		}

		public void Release()
		{
			lock (_lock)
			{
				ObjectDisposedException.ThrowIf(_timer is null, typeof(PollingScheduler));
				_logger.SensorServicePollingSchedulerRelease(_referenceCount);
				if (--_referenceCount == 0)
				{
					_timer.Change(Timeout.Infinite, _period);
					_tickSignal.TrySetCanceled();
					Volatile.Write(ref _tickSignal, NonAcquiredTickSignal);
					_logger.SensorServicePollingSchedulerDisabled();
				}
			}
		}

		public void Dispose()
		{
			lock (_lock)
			{
				if (Interlocked.Exchange(ref _timer, null) is { } timer)
				{
					timer.Dispose();
					_tickSignal.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(PollingScheduler).FullName)));
				}
			}
		}

		public Task WaitAsync(CancellationToken cancellationToken) => _tickSignal.Task.WaitAsync(cancellationToken);
	}
}
