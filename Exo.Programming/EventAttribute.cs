namespace Exo.Programming;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class EventAttribute : Attribute
{
	public EventAttribute(string name, uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
	{
		Name = name;
		Id = new(a, b, c, d, e, f, g, h, i, j, k);
	}

	public Guid Id { get; }
	public string Name { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class EventAttribute<T> : EventAttribute
	where T : notnull, EventParameters
{
	public EventAttribute(string name, uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k) : base(name, a, b, c, d, e, f, g, h, i, j, k)
	{
	}
}
