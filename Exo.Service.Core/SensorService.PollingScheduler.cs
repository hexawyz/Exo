using System.Runtime.ExceptionServices;

namespace Exo.Service;

public sealed partial class SensorService
{
	// The scheduler will tick at the requested rate when enabled.
	// It is used as a synchronization source for all polled sensors.
	private sealed class PollingScheduler : IDisposable
	{
		private TaskCompletionSource _tickSignal;
		private readonly object _lock;
		private int _referenceCount;
		private readonly int _period;
		private Timer? _timer;

		public PollingScheduler(int period)
		{
			_lock = new();
			_tickSignal = new();
			_period = period;
			_timer = new Timer(OnTick, null, Timeout.Infinite, period);
		}

		private void OnTick(object? state)
		{
			_tickSignal.TrySetResult();
			Volatile.Write(ref _tickSignal, new());
		}

		public void Acquire()
		{
			lock (_lock)
			{
				ObjectDisposedException.ThrowIf(_timer is null, typeof(PollingScheduler));
				if (_referenceCount == 0)
				{
					_timer.Change(0, _period);
				}
				_referenceCount++;
			}
		}

		public void Release()
		{
			lock (_lock)
			{
				ObjectDisposedException.ThrowIf(_timer is null, typeof(PollingScheduler));
				if (--_referenceCount == 0)
				{
					_timer.Change(Timeout.Infinite, _period);
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
