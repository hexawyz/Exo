using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

// The scheduler will tick at the requested rate when enabled.
// It is used as a synchronization source for all polled sensors.
internal sealed class PollingScheduler : IAsyncDisposable
{
	// Use a linked list to register waiting states. Not really for performance, but to reduce memory usage in the idle case.
	// Alternative would be to have a growing array with pre-allocated states, but we would need to identify opportunities to degrow.
	// Also, we'll need to access the state's contents in order to schedule continuations, so the linked list might have less enumeration overhead than an array.
	private WatcherState? _head;
	// The lock is used to make cleanup easier, as removing events would
	private readonly Lock _lock;
	private PeriodicTimer? _timer;
	private readonly TimeSpan _period;
	private readonly ILogger<PollingScheduler> _logger;
	private readonly Task _runTask;

	public PollingScheduler(ILogger<PollingScheduler> logger, TimeSpan period)
	{
		_logger = logger;
		_period = period;
		_lock = new();
		_timer = new PeriodicTimer(Timeout.InfiniteTimeSpan);
		_runTask = RunAsync(_timer);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _timer, null) is { } timer)
		{
			timer.Dispose();
			lock (_lock)
			{
				var current = _head;
				while (current is not null)
				{
					current.Dispose();
				}
				Volatile.Write(ref _head, null);
			}
			await _runTask.ConfigureAwait(false);
		}
	}

	private async Task RunAsync(PeriodicTimer timer)
	{
		while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
		{
			SignalAndCleanup(timer);
		}
	}

	private void SignalAndCleanup(PeriodicTimer timer)
	{
		lock (_lock)
		{
			// Track the current position in the list as well as the position of the first item to be removed if necessary.
			// If current and start are equal, there are no elements to remove.
			ref WatcherState? current = ref _head;
			ref WatcherState? start = ref current;
			if (current is not null)
			{
				while (true)
				{
					ref var next = ref current._next;
					if (current.Signal())
					{
						if (!Unsafe.AreSame(ref start, ref current)) Volatile.Write(ref start, current);
						if (next is null) break;
						start = ref next;
					}
					else
					{
						_logger.PollingSchedulerRelease();
						if (next is null)
						{
							if (!Unsafe.AreSame(ref start, ref current)) Volatile.Write(ref start, null);
							break;
						}
					}
					current = ref next;
				}
				if (Volatile.Read(ref _head) is null)
				{
					timer.Period = Timeout.InfiniteTimeSpan;
					_logger.PollingSchedulerDisabled();
				}
			}
		}
	}

	public TickWaiter StartTicking()
	{
		lock (_lock)
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(ref _timer) is null, this);
			_logger.PollingSchedulerAcquire();
			var state = new WatcherState();
			ref WatcherState? next = ref _head;
			WatcherState? current = null;
			while ((current = Interlocked.CompareExchange(ref next, state, null)) is not null)
			{
				next = ref current._next;
			}
			if (Unsafe.AreSame(ref next, ref _head) && _timer is { } timer)
			{
				timer.Period = _period;
				_logger.PollingSchedulerEnabled();
			}
			return new(state);
		}
	}

	private sealed class WatcherState : IValueTaskSource<bool>
	{
		private const int StateReady = 0;
		private const int StateCompleted = 1;
		private const int StateDisposed = 2;

		internal WatcherState? _next;
		private ManualResetValueTaskSourceCore<bool> _core;
		private byte _state;

		public WatcherState()
		{
			_core.RunContinuationsAsynchronously = true;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _state, StateDisposed) == StateReady)
			{
				_core.SetResult(false);
			}
		}

		public bool Signal()
		{
			switch (Interlocked.CompareExchange(ref _state, StateCompleted, StateReady))
			{
			case StateReady:
				_core.SetResult(true);
				goto case StateCompleted;
			case StateCompleted:
				return true;
			default:
				return false;
			}
		}

		public ValueTask<bool> WaitAsync()
			=> new(this, _core.Version);

		bool IValueTaskSource<bool>.GetResult(short token)
		{
			bool result = _core.GetResult(token);
			switch (Interlocked.CompareExchange(ref _state, StateReady, StateCompleted))
			{
			case StateReady:
				// This state should never be reached. We can consider the object to be disposed.
				goto case StateDisposed;
			case StateCompleted:
				_core.Reset();
				return result;
			case StateDisposed:
			default:
				return false;
			}
		}

		ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
			=> _core.GetStatus(token);

		void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
			=> _core.OnCompleted(continuation, state, token, flags);
	}

	public readonly struct TickWaiter : IDisposable
	{
		private readonly WatcherState? _state;

		internal TickWaiter(object state) => _state = (WatcherState)state;

		public void Dispose() => _state!.Dispose();

		public ValueTask<bool> WaitAsync() => _state!.WaitAsync();

		public bool IsDefault => _state is null;
	}
}

internal sealed class PollingSchedulerDisabledException : Exception
{
	public PollingSchedulerDisabledException() : base("The scheduler is disabled.") { }
}
