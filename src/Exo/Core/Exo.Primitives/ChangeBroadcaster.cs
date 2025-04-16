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
	/// <returns>A read-only snapshot functionally equivalent to this instance for pushing values.</returns>
	public ChangeBroadcasterSnapshot<T> GetSnapshot() => new(Volatile.Read(ref _listeners));

	public void Register(ChannelWriter<T> writer)
	{
		object? listeners = Interlocked.CompareExchange(ref _listeners, writer, null);
		if (listeners is null) return;

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
			if (ReferenceEquals(listeners, listeners = Interlocked.CompareExchange(ref _listeners, newListeners, listeners))) return;
			if (listeners is null)
			{
				listeners = Interlocked.CompareExchange(ref _listeners, writer, null);
				if (listeners is null) return;
			}
		}
	}

	public void Unregister(ChannelWriter<T> writer)
	{
		while (true)
		{
			object? listeners = Interlocked.CompareExchange(ref _listeners, null, writer);
			if (ReferenceEquals(listeners, writer) || listeners is null || listeners is ChannelWriter<T>) return;

			while (true)
			{
				ChannelWriter<T>[]? newListeners;
				newListeners = Unsafe.As<ChannelWriter<T>[]>(listeners);
				int index = Array.IndexOf(newListeners, writer);
				if (index < 0) return;

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
				if (ReferenceEquals(listeners, listeners = Interlocked.CompareExchange(ref _listeners, newListeners, listeners)) || listeners is null) return;
				if (ReferenceEquals(listeners, writer)) break;
				if (listeners is ChannelWriter<T>) return;
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
	public void UnregisterWatcher(ChannelWriter<T> writer);
}

// A light abstraction for watching values. Exposing the channel reader allows smoother 
public struct BroadcastedChangeWatcher<T> : IDisposable
{
	public static async ValueTask<BroadcastedChangeWatcher<T>> CreateAsync(IChangeSource<T> source, CancellationToken cancellationToken)
	{
		var channel = Channel.CreateUnbounded<T>(new() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });
		var initialData = await source.GetInitialChangesAndRegisterWatcherAsync(channel, cancellationToken).ConfigureAwait(false);
		return new(source, initialData, channel);
	}

	private readonly IChangeSource<T> _source;
	private T[]? _initialData;
	private readonly Channel<T> _channel;

	private BroadcastedChangeWatcher(IChangeSource<T> source, T[]? initialData, Channel<T> channel)
	{
		_source = source;
		_channel = channel;
		_initialData = initialData;
	}

	public void Dispose() => _source.UnregisterWatcher(_channel.Writer);

	public T[]? ConsumeInitialData() => Interlocked.Exchange(ref _initialData, null);
	public ChannelReader<T> Reader => _channel.Reader;
}
