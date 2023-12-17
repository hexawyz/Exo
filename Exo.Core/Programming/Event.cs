using System.Runtime.CompilerServices;

namespace Exo.Programming;

// TODO: The memory usage pattern of events could perhaps be improved, and maybe events could be strongly typed using a type per event.
// In that case, events would expose the TypeId attribute, and the EventAttribute would reference the type instead of providing ID and name.
// Only problem with that would be for very simple events, where it would involve the creation of many simple and identical types.
public readonly struct Event
{
	public static Event Create(Guid eventId) => new(eventId);
	public static Event<T> Create<T>(Guid eventId, T parameters) where T : notnull, EventParameters => new(eventId, parameters);

	public Event(Guid eventId) => EventId = eventId;

	public Guid EventId { get; }
	public object? Parameters { get; }
}

public readonly struct Event<T>
	where T : notnull, EventParameters
{
	public Event(Guid eventId, T parameters)
	{
		EventId = eventId;
		Parameters = parameters;
	}

	public Guid EventId { get; }
	public T Parameters { get; }

	public static implicit operator Event(Event<T> @event) => Unsafe.As<Event<T>, Event>(ref @event);

	public static explicit operator Event<T>(Event @event)
	{
		if (@event.Parameters is not Event<T>) throw new InvalidCastException();
		return Unsafe.As<Event, Event<T>>(ref @event);
	}
}
