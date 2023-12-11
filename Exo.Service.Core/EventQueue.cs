using System.Threading.Channels;
using Exo.Programming;

namespace Exo.Service;

// The event queue will take in any event in form of a GUID, and provide events sequentially.
public sealed class EventQueue
{
	private static readonly UnboundedChannelOptions ChannelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false };

	private readonly Channel<Event> _channel = Channel.CreateUnbounded<Event>(ChannelOptions);

	public EventQueue() { }

	//public void RegisterEvent(string name) where TEvent : Delegate { }

	//public void GetEvent();

	public ChannelWriter<Event> Writer => _channel.Writer;
	public ChannelReader<Event> Reader => _channel.Reader;
}
