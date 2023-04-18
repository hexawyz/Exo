using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Exo;

/// <summary>Represents a global mutex that can be used within async code.</summary>
/// <remarks>
/// <para>
/// This abstraction allows acquiring and releasing the mutex exclusively, in an asynchronous context.
/// It also allows running code within the thread of the mutex, so as to preserve thread affinity when needed.
/// Thread affinity is especially important for mutexes that could be used in a reentrant way. (e.g. acquired both by managed code and native code called by P/Invoke)
/// In all cases, thread affinity will be lost once the mutex is released, by disposing of the <see cref="IOwnedMutex"/> instance.
/// Global mutexes are not re-entrant on the managed side. Trying to acquire the mutex multiple times in a row will generate a deadlock situation.
/// </para>
/// <para>
/// Once a global mutex is accessed, it stays alive for the whole duration of the process.
/// This shouldn't be a problem, as global mutexes are well-known and not large in number.
/// While the <see cref="Get"/> method is left public for now, its use should be discouraged.
/// All well-know mutexes should be exposed as singleton, either on this class or on other third-party classes, as required by the situation.
/// </para>
/// <para>
/// Mutexes are instantiated lazily, as not every Mutex may be needed in a given run.
/// </para>
/// </remarks>
public sealed class AsyncGlobalMutex
{
	internal class OwnedMutexTaskScheduler : TaskScheduler
	{
		// The owner will be cleared when the object is disposed.
		private AsyncGlobalMutex? _owner;
		private readonly TaskScheduler _fallbackScheduler;

		public OwnedMutexTaskScheduler(AsyncGlobalMutex owner, TaskScheduler fallbackScheduler)
		{
			_owner = owner;
			_fallbackScheduler = fallbackScheduler;
		}

		internal void OnOwnerFinalized() => MarkAsDisposed();

		private void MarkAsDisposed()
		{
			if (Interlocked.Exchange(ref _owner, null) is { } owner)
			{
				owner._manualResetEvent.Set();
			}
		}

		public ValueTask DisposeAsync()
		{
			MarkAsDisposed();
			return ValueTask.CompletedTask;
		}

		public bool IsDisposed => Volatile.Read(ref _owner) is null;

		protected override IEnumerable<Task>? GetScheduledTasks()
		{
			if (Volatile.Read(ref _owner) is { } owner)
			{
				return owner._pendingTaskList.ToArray();
			}
			else
			{
				return Array.Empty<Task>();
			}
		}

		protected override void QueueTask(Task task)
		{
			if (Volatile.Read(ref _owner) is { } owner)
			{
				owner._pendingTaskList.Enqueue(task);
				owner._manualResetEvent?.Set();
			}
			else
			{
				ExecuteOnFallbackScheduler(task);
			}
		}

		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
		{
			if (ReferenceEquals(Thread.CurrentThread, _owner?._thread))
			{
				return TryExecuteTask(task);
			}
			return false;
		}

		internal void ProcessQueuedTask(Task task)
		{
			// All tasks that are still queued once the scheduler is disposed must be forwarded to the fallback scheduler.
			if (Volatile.Read(ref _owner) is not null)
			{
				TryExecuteTask(task);
			}
			else
			{
				ExecuteOnFallbackScheduler(task);
			}
		}

		private void ExecuteOnFallbackScheduler(Task task)
			=> _ = Task.Factory.StartNew(s => TryExecuteTask((Task)s!), task, CancellationToken.None, TaskCreationOptions.None, _fallbackScheduler);

		internal void EnqueueAction(Action action)
			=> Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, Volatile.Read(ref _owner) is not null ? this : _fallbackScheduler);
	}

	// Declare some of the well-known global mutex names.
	// Some are probably missing, let's add them alter on.
	public const string SmBusMutexName = "Global\\Access_SMBUS.HTP.Method";
	public const string IsaBusMutexName = "Global\\Access_ISABUS.HTP.Method";
	public const string EmbeddedControllerMutexName = "Global\\Access_EC";

	// Singleton implementations for each well-known global mutex.
	private static class SmBusAsyncGlobalMutex
	{
		internal static readonly AsyncGlobalMutex Instance = Get(SmBusMutexName);
	}

	private static class IsaBusAsyncGlobalMutex
	{
		internal static readonly AsyncGlobalMutex Instance = Get(IsaBusMutexName);
	}

	private static class EmbeddedControllerGlobalMutex
	{
		internal static readonly AsyncGlobalMutex Instance = Get(EmbeddedControllerMutexName);
	}

	public static AsyncGlobalMutex SmBus => SmBusAsyncGlobalMutex.Instance;

	public static AsyncGlobalMutex IsaBus => IsaBusAsyncGlobalMutex.Instance;

	public static AsyncGlobalMutex EmbeddedController => EmbeddedControllerGlobalMutex.Instance;

	[EditorBrowsable(EditorBrowsableState.Never)]
	/// <summary>Gets the global mutex with the specified name.</summary>
	/// <remarks>
	/// Please avoid calling this method directly, and prefer relying on a well-known singleton instead.
	/// This method is designed for extensibility and reusability, in case some global singletons would not be exposed here.
	/// However, the intent is for this method to be considered as private.
	/// </remarks>
	/// <param name="name"></param>
	/// <returns></returns>
	public static AsyncGlobalMutex Get(string name)
		=> Mutexes.GetOrAdd(name, n => new AsyncGlobalMutex(name));

	private static readonly ConcurrentDictionary<string, AsyncGlobalMutex> Mutexes = new ConcurrentDictionary<string, AsyncGlobalMutex>();

	private readonly Mutex _mutex;
	private readonly Thread _thread;
	private readonly ConcurrentQueue<Task> _pendingTaskList;
	private readonly ConcurrentQueue<(TaskCompletionSource<OwnedMutex> TaskCompletionSource, IMutexLifetime? lifecycle, TaskScheduler fallbackScheduler)> _waitQueue;
	private readonly ManualResetEventSlim _manualResetEvent;

	private AsyncGlobalMutex(string mutexName)
	{
		_mutex = new(false, mutexName);
		_thread = new(MutexThread);
		_pendingTaskList = new();
		_waitQueue = new();
		_manualResetEvent = new(false);
	}

	/// <summary>Acquires the global mutex.</summary>
	/// <remarks>
	/// <para>
	/// The global mutex will be associated with a task scheduler that can be used to schedule tasks on the thread used to acquire and release the mutex.
	/// Client code can switch to this thread by awaiting the result of <see cref="OwnedMutex.ContinueOnScheduler"/>.
	/// </para>
	/// <para>
	/// Continuing on the mutex thread is useful when running native code that is susceptible of acquiring the mutex itself.
	/// </para>
	/// </remarks>
	/// <param name="captureScheduler">Determines if the current task scheduler or synchronization context must be captured as a fallback to the Mutex task scheduler.</param>
	/// <returns>An object that must be disposed to release the mutex.</returns>
	public Task<OwnedMutex> AcquireAsync(bool captureScheduler = false)
		=> EnterAsyncCore(null, captureScheduler);

	public Task<OwnedMutex> AcquireAsync(IMutexLifetime lifecycle, bool captureScheduler)
		=> EnterAsyncCore(lifecycle ?? throw new ArgumentNullException(nameof(lifecycle)), captureScheduler);

	private Task<OwnedMutex> EnterAsyncCore(IMutexLifetime? lifecycle, bool captureScheduler)
	{
		TaskScheduler taskScheduler;
		if (captureScheduler)
		{
			taskScheduler = TaskScheduler.Current;
			if (ReferenceEquals(taskScheduler, TaskScheduler.Default) && SynchronizationContext.Current is not null)
			{
				taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
			}
		}
		else
		{
			taskScheduler = TaskScheduler.Default;
		}
		var tcs = new TaskCompletionSource<OwnedMutex>();
		_waitQueue.Enqueue((tcs, lifecycle, taskScheduler));
		_manualResetEvent.Set();
		return tcs.Task;
	}

	private void MutexThread()
	{
		while (true)
		{
			_manualResetEvent.Wait();
			while (_waitQueue.TryDequeue(out var tuple))
			{
				var (tcs, lifecycle, fallbackScheduler) = tuple;

				_mutex.WaitOne();

				var scheduler = new OwnedMutexTaskScheduler(this, fallbackScheduler);

				try
				{
					lifecycle?.OnAfterAcquire();
				}
				catch (Exception ex)
				{
					tcs.TrySetException(ex);

					_manualResetEvent.Reset();

					continue;
				}

				while (true)
				{
					_manualResetEvent.Reset();
					while (_pendingTaskList.TryDequeue(out var task))
					{
						scheduler.ProcessQueuedTask(task);
					}
					if (scheduler.IsDisposed)
					{
						break;
					}
					_manualResetEvent.Wait();
				}

				try
				{
					lifecycle?.OnBeforeRelease();
				}
				catch
				{
					// There is no good way to propagate an exception here…
				}

				_manualResetEvent.Reset();
			}
		}
	}
}

public sealed class OwnedMutex : IAsyncDisposable
{
	private readonly AsyncGlobalMutex.OwnedMutexTaskScheduler _taskScheduler;

	internal OwnedMutex(AsyncGlobalMutex.OwnedMutexTaskScheduler taskScheduler) => _taskScheduler = taskScheduler;

	~OwnedMutex()
	{
		// Log an error here.
		_taskScheduler.OnOwnerFinalized();
	}

	public async ValueTask DisposeAsync()
	{
		await _taskScheduler.DisposeAsync();
		GC.SuppressFinalize(this);
	}

	public OwnedMutexAwaitable ContinueOnScheduler() => new(_taskScheduler);
}

public readonly struct OwnedMutexAwaitable
{
	private readonly AsyncGlobalMutex.OwnedMutexTaskScheduler _taskScheduler;

	internal OwnedMutexAwaitable(AsyncGlobalMutex.OwnedMutexTaskScheduler taskScheduler) => _taskScheduler = taskScheduler;

	public OwnedMutexAwaiter GetAwaiter() => new(_taskScheduler);
}

public readonly struct OwnedMutexAwaiter : INotifyCompletion
{
	private readonly AsyncGlobalMutex.OwnedMutexTaskScheduler _taskScheduler;

	internal OwnedMutexAwaiter(AsyncGlobalMutex.OwnedMutexTaskScheduler taskScheduler) => _taskScheduler = taskScheduler;

	public void OnCompleted(Action continuation)
		=> _taskScheduler.EnqueueAction(continuation);
}
