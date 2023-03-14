using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DeviceTools.Logitech.HidPlusPlus;

internal struct LightweightSingleProducerSingleConsumerQueue<T> : IDisposable
{
	private static readonly object DisposedSignal = new();

	private readonly ConcurrentQueue<T> _queue = new();
	private object? _signal;

	public LightweightSingleProducerSingleConsumerQueue()
	{
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _signal, DisposedSignal) is TaskCompletionSource tcs)
		{
			tcs.TrySetException(new ObjectDisposedException(nameof(LightweightSingleProducerSingleConsumerQueue<T>)));
		}
	}

	public void Enqueue(T item)
	{
		_queue.Enqueue(item);
		(Volatile.Read(ref _signal) as TaskCompletionSource)?.TrySetResult();
	}

	public ValueTask<T> DequeueAsync()
	{
		if (_queue.TryDequeue(out var item))
		{
			return new ValueTask<T>(item);
		}
		else
		{
			return SlowDequeueAsync();
		}
	}

	private ValueTask<T> SlowDequeueAsync()
	{
		var signal = Volatile.Read(ref _signal);

		if (ReferenceEquals(signal, DisposedSignal)) goto ThrowObjectDisposedException;

		TaskCompletionSource tcs;

		if (signal is not null)
		{
			tcs = Unsafe.As<TaskCompletionSource>(signal);

			if (!tcs.Task.IsCompleted) goto TryDequeueBeforeWait;
			else if (tcs.Task.IsFaulted) goto AwaitAndDequeue;
		}

		tcs = new TaskCompletionSource();

		if (ReferenceEquals(Interlocked.CompareExchange(ref _signal, tcs, signal), DisposedSignal)) goto ThrowObjectDisposedException;

		TryDequeueBeforeWait:;
		if (_queue.TryDequeue(out var item)) return new ValueTask<T>(item);

		AwaitAndDequeue:;
		return new ValueTask<T>(AwaitAndDequeueAsync(tcs.Task));

	ThrowObjectDisposedException:;
		return ValueTask.FromException<T>(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(nameof(LightweightSingleProducerSingleConsumerQueue<T>))));
	}

	private async Task<T> AwaitAndDequeueAsync(Task task)
	{
		await task.ConfigureAwait(false);
		if (_queue.TryDequeue(out var item)) return item;
		else throw new InvalidOperationException("Expected an item to be queued.");
	}
}
