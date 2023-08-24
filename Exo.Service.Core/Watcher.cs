using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Exo.Service;

internal static class Watcher
{
	private static readonly UnboundedChannelOptions SingleWriterWatchChannelOptions = new() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false };
	private static readonly UnboundedChannelOptions MultiWriterWatchChannelOptions = new() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false };

	public static Channel<T> CreateSingleWriterChannel<T>()
		where T : notnull
		=> Channel.CreateUnbounded<T>(SingleWriterWatchChannelOptions);

	public static Channel<T> CreateChannel<T>()
		where T : notnull
		=> Channel.CreateUnbounded<T>(MultiWriterWatchChannelOptions);
}

public abstract class Watcher<TKey, TValue> : IAsyncDisposable
	where TKey : notnull
	where TValue : notnull
{
	private readonly ConcurrentDictionary<TKey, TValue> _currentStates;
	private ChannelWriter<TValue>[]? _changeListeners;
	private object? _lock;
	private TaskCompletionSource<CancellationToken> _startRunTaskCompletionSource;
	private CancellationTokenSource? _currentRunCancellationTokenSource;
	private int _watcherCount;
	private readonly CancellationTokenSource _disposeCancellationTokenSource;
	private readonly Task _watchTask;

	public Watcher()
	{
		_currentStates = new();
		_lock = new();
		_startRunTaskCompletionSource = new();
		_disposeCancellationTokenSource = new();
		_watchTask = WatchDevicesAsync(_disposeCancellationTokenSource.Token);
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
			ObjectDisposedException.ThrowIf(@lock is null, GetType());
			return @lock;
		}
	}

	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				var currentRunCancellation = await Volatile.Read(ref _startRunTaskCompletionSource).Task.ConfigureAwait(false);

				// This loop can be canceled
				try
				{
					await WatchAsyncCore(currentRunCancellation).ConfigureAwait(false);
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

	protected abstract Task WatchAsyncCore(CancellationToken cancellationToken);

	protected bool Add(TKey key, TValue value)
	{
		lock (_lock!)
		{
			if (_currentStates.TryAdd(key, value))
			{
				_changeListeners.TryWrite(value);
				return true;
			}
		}
		return false;
	}

	protected bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
		=> _currentStates.TryGetValue(key, out value);

	protected bool Remove(TKey key)
	{
		lock (_lock!)
		{
			return _currentStates.TryRemove(key, out _);
		}
	}

	protected bool TryUpdate(TKey key, TValue newValue, TValue comparisonValue)
	{
		if (_currentStates.TryUpdate(key, newValue, comparisonValue))
		{
			Volatile.Read(ref _changeListeners).TryWrite(newValue);
			return true;
		}
		return false;
	}

	public async IAsyncEnumerable<TValue> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ChannelReader<TValue> reader;

		var channel = Watcher.CreateChannel<TValue>();
		reader = channel.Reader;
		var writer = channel.Writer;

		TValue[]? initialValues;
		var @lock = Lock;
		lock (@lock)
		{
			initialValues = _currentStates.Values.ToArray();

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
			foreach (var state in initialValues)
			{
				yield return state;
			}
			initialValues = null;

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
