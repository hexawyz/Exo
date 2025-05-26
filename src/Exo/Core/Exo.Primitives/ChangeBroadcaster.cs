using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Exo.Primitives;

/// <summary>A reusable change broadcast backend.</summary>
/// <remarks>
/// This is intended to be used as a mutable field in a class, and will provide a low-overhead broadcast mechanism.
/// Listeners are <see cref="ChannelWriter{T}"/> instances that will receive all notifications while they are registered.
/// Notifications are pushed by calling <see cref="Push(T)"/>.
/// </remarks>
/// <typeparam name="T"></typeparam>
public struct ChangeBroadcaster<T>
{
	private object? _listeners;

	public void Push(T value)
	{
		var listeners = Volatile.Read(ref _listeners);
		if (listeners is null) return;
		if (listeners is ChannelWriter<T> writer)
		{
			writer.TryWrite(value);
		}
		else
		{
			foreach (var writer2 in Unsafe.As<ChannelWriter<T>[]>(listeners))
			{
				writer2.TryWrite(value);
			}
		}
	}

	/// <summary>Captures a snapshot of the change broadcaster so that events can be pushed conditionally.</summary>
	/// <remarks>
	/// Snapshots must be short lived and serve the main purpose of checking if the watchers list is empty before pushing a message.
	/// Any use of a snapshot falls under the same calling restrictions as the main <see cref="ChangeBroadcaster{T}"/>.
	/// </remarks>
	/// <returns>A read-only snapshot functionally equivalent to this instance for pushing values.</returns>
	public ChangeBroadcasterSnapshot<T> GetSnapshot() => new(Volatile.Read(ref _listeners));

	/// <summary>Tries to complete all listeners at once.</summary>
	/// <remarks>This must not be called concurrently with code calling the <see cref="Push(T)"/> method.</remarks>
	/// <param name="error">The error to optionally signal to the watchers.</param>
	public void TryComplete(Exception? error = null)
	{
		var listeners = Interlocked.Exchange(ref _listeners, null);
		if (listeners is null) return;
		if (listeners is ChannelWriter<T> writer)
		{
			writer.TryComplete(error);
		}
		else
		{
			foreach (var writer2 in Unsafe.As<ChannelWriter<T>[]>(listeners))
			{
				writer2.TryComplete(error);
			}
		}
	}

	/// <summary>Registers a channel writer to watch on values.</summary>
	/// <param name="writer"></param>
	/// <returns><see langword="true"/> if the writer was the first one after being registered. Otherwise, <see langword="false"/>.</returns>
	public bool Register(ChannelWriter<T> writer)
	{
		object? listeners = Interlocked.CompareExchange(ref _listeners, writer, null);
		if (listeners is null) return true;

		while (true)
		{
			ChannelWriter<T>[]? newListeners;
			if (listeners is ChannelWriter<T> otherWriter)
			{
				newListeners = [otherWriter, writer];
			}
			else
			{
				newListeners = [.. Unsafe.As<ChannelWriter<T>[]>(listeners), writer];
			}
			if (ReferenceEquals(listeners, listeners = Interlocked.CompareExchange(ref _listeners, newListeners, listeners))) return false;
			if (listeners is null)
			{
				listeners = Interlocked.CompareExchange(ref _listeners, writer, null);
				if (listeners is null) return true;
			}
		}
	}

	/// <summary>Unregisters a channel writer from watching on values.</summary>
	/// <param name="writer"></param>
	/// <returns><see langword="true"/> if the writer was the last one before being unregistered. Otherwise, <see langword="false"/>.</returns>
	public bool Unregister(ChannelWriter<T> writer)
	{
		while (true)
		{
			object? listeners = Interlocked.CompareExchange(ref _listeners, null, writer);
			if (ReferenceEquals(listeners, writer)) return true;
			if (listeners is null || listeners is ChannelWriter<T>) return false;

			while (true)
			{
				ChannelWriter<T>[]? newListeners;
				newListeners = Unsafe.As<ChannelWriter<T>[]>(listeners);
				int index = Array.IndexOf(newListeners, writer);
				if (index < 0) return false;

				int newLength = newListeners.Length - 1;

				if (newLength == 0)
				{
					newListeners = null;
				}
				else
				{
					newListeners = new ChannelWriter<T>[newLength];
					Array.Copy(Unsafe.As<ChannelWriter<T>[]>(listeners), 0, newListeners, 0, index);
					Array.Copy(Unsafe.As<ChannelWriter<T>[]>(listeners), index + 1, newListeners, index, newLength - index);
				}
				if (ReferenceEquals(listeners, listeners = Interlocked.CompareExchange(ref _listeners, newListeners, listeners))) return newListeners is null;
				if (listeners is null) return false;
				if (ReferenceEquals(listeners, writer)) break;
				if (listeners is ChannelWriter<T>) return false;
			}
		}
	}
}

public readonly struct ChangeBroadcasterSnapshot<T>
{
	private readonly object? _listeners;

	internal ChangeBroadcasterSnapshot(object? listeners) => _listeners = listeners;

	public bool IsEmpty => _listeners is null;

	public void Push(T value)
	{
		var listeners = _listeners;
		if (listeners is null) return;
		if (listeners is ChannelWriter<T> writer)
		{
			writer.TryWrite(value);
		}
		else
		{
			foreach (var writer2 in Unsafe.As<ChannelWriter<T>[]>(listeners))
			{
				writer2.TryWrite(value);
			}
		}
	}
}

public interface IChangeSource<T>
{
	public ValueTask<T[]?> GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<T> writer, CancellationToken cancellationToken);
	/// <summary>Unregisters the writer without ensuring that all messages have been written.</summary>
	/// <remarks>
	/// In the normal completion path, we know for sure that we are called once the reader is no longer in use.
	/// In this scenario, no lock is acquired and the channel cannot (must not) be completed.
	/// </remarks>
	/// <param name="writer"></param>
	public void UnsafeUnregisterWatcher(ChannelWriter<T> writer);
	/// <summary>Unregisters and completes the writer after ensuring that all messages have been written.</summary>
	/// <remarks>
	/// In the cancellation path, which is very likely, we want to complete the channel so that we can avoid throwing an <see cref="OperationCanceledException"/>.
	/// This means that a lock needs to be acquired in order to guarantee that there is no conflicting use of the writer.
	/// </remarks>
	/// <param name="writer"></param>
	/// <returns></returns>
	public ValueTask SafeUnregisterWatcherAsync(ChannelWriter<T> writer);
}

// A light abstraction for watching values. Exposing the channel reader allows smoother processing.
public struct BroadcastedChangeWatcher<T> : IDisposable
{
	public static ValueTask<BroadcastedChangeWatcher<T>> CreateAsync(IChangeSource<T> source, CancellationToken cancellationToken)
		=> CreateAsync(source, new UnboundedChannelOptions() { SingleWriter = true, SingleReader = true, AllowSynchronousContinuations = false }, cancellationToken);

	public static async ValueTask<BroadcastedChangeWatcher<T>> CreateAsync(IChangeSource<T> source, UnboundedChannelOptions options, CancellationToken cancellationToken)
	{
		var channel = Channel.CreateUnbounded<T>(options);
		var initialData = await source.GetInitialChangesAndRegisterWatcherAsync(channel, cancellationToken).ConfigureAwait(false);
		var registration = cancellationToken.CanBeCanceled ?
			cancellationToken.UnsafeRegister
			(
				static async (state) =>
				{
					var (source, channel) = (Tuple<IChangeSource<T>, Channel<T>>)state!;
					try
					{
						await source.SafeUnregisterWatcherAsync(channel.Writer).ConfigureAwait(false);
					}
					catch
					{
					}
				},
				Tuple.Create(source, channel)
			) :
			default;
		return new(source, initialData, channel, registration);
	}

	public static ValueTask<BroadcastedChangeWatcher<T>> CreateAsync(IChangeSource<T> source, int capacity, BoundedChannelFullMode fullMode, CancellationToken cancellationToken)
		=> CreateAsync
		(
			source,
			new BoundedChannelOptions(capacity)
			{
				SingleWriter = true,
				SingleReader = true,
				AllowSynchronousContinuations = false,
				FullMode = fullMode,
			},
			cancellationToken
		);

	public static async ValueTask<BroadcastedChangeWatcher<T>> CreateAsync(IChangeSource<T> source, BoundedChannelOptions options, CancellationToken cancellationToken)
	{
		var channel = Channel.CreateBounded<T>(options);
		var initialData = await source.GetInitialChangesAndRegisterWatcherAsync(channel, cancellationToken).ConfigureAwait(false);
		var registration = cancellationToken.CanBeCanceled ?
			cancellationToken.UnsafeRegister
			(
				static async (state) =>
				{
					var (source, channel) = (Tuple<IChangeSource<T>, Channel<T>>)state!;
					try
					{
						await source.SafeUnregisterWatcherAsync(channel.Writer).ConfigureAwait(false);
					}
					catch
					{
					}
				},
				Tuple.Create(source, channel)
			) :
			default;
		return new(source, initialData, channel, registration);
	}

	private readonly IChangeSource<T> _source;
	private T[]? _initialData;
	private readonly Channel<T> _channel;
	private readonly CancellationTokenRegistration _registration;

	private BroadcastedChangeWatcher(IChangeSource<T> source, T[]? initialData, Channel<T> channel, CancellationTokenRegistration registration)
	{
		_source = source;
		_channel = channel;
		_initialData = initialData;
		_registration = registration;
	}

	public void Dispose()
	{
		_source.UnsafeUnregisterWatcher(_channel.Writer);
		_registration.Dispose();
	}

	public T[]? ConsumeInitialData() => Interlocked.Exchange(ref _initialData, null);
	public ChannelReader<T> Reader => _channel.Reader;
}
