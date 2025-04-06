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
