using System.Collections;
using System.Threading.Channels;

namespace Exo.Rpc;

/// <summary>Provides the feature for transmissions that can be reset.</summary>
/// <remarks>
/// Consumers should enumerate the readers and consider the completion of a channel as a signal that their state should be reset.
/// </remarks>
/// <typeparam name="T">The type of element in the channel.</typeparam>
public sealed class ResettableChannel<T> : IEnumerable<ChannelReader<T>>, IDisposable
{
	private readonly UnboundedChannelOptions _channelOptions;
	private Channel<T>? _channel;

	public ResettableChannel(UnboundedChannelOptions channelOptions)
	{
		_channelOptions = channelOptions;
		_channel = Channel.CreateUnbounded<T>(channelOptions);
	}

	~ResettableChannel() => Dispose(false);

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public void Dispose(bool disposing)
	{
		Interlocked.Exchange(ref _channel, null)?.Writer.TryComplete();
	}

	public IEnumerator<ChannelReader<T>> EnumerateReaders()
	{
		while (Volatile.Read(ref _channel) is { } channel)
		{
			yield return channel;
		}
	}

	public ChannelWriter<T> CurrentWriter => (_channel ?? throw new ObjectDisposedException(GetType().FullName)).Writer;

	public void Reset()
	{
		ObjectDisposedException.ThrowIf(_channel is null, this);
		Interlocked.Exchange(ref _channel, Channel.CreateUnbounded<T>(_channelOptions))!.Writer.TryComplete();
	}

	IEnumerator<ChannelReader<T>> IEnumerable<ChannelReader<T>>.GetEnumerator() => EnumerateReaders();
	IEnumerator IEnumerable.GetEnumerator() => EnumerateReaders();
}
