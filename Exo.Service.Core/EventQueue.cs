using System.Threading.Channels;

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

// TODO: The memory usage pattern of events could perhaps be improved, and maybe events could be strongly typed using a type per event.
// In that case, events would expose the TypeId attribute, and the EventAttribute would reference the type instead of providing ID and name.
// Only problem with that would be for very simple events, where it would involve the creation of many simple and identical types.
public /*abstract*/ class Event
{
	public Event(Guid eventId) => EventId = eventId;

	public /*abstract*/ Guid EventId { get; }
}

public /*abstract*/ class Event<T> : Event
{
	public Event(Guid eventId, T parameterValue) : base(eventId)
	{
		ParameterValue = parameterValue;
	}

	public /*abstract*/ T ParameterValue { get; }
}
