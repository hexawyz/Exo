namespace Exo.Programming;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class EventAttribute : Attribute
{
	public EventAttribute(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k, string name)
	{
		Id = new(a, b, c, d, e, f, g, h, i, j, k);
		Name = name;
	}

	public Guid Id { get; }
	public string Name { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class EventAttribute<T> : Attribute
{
	public EventAttribute(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k, string name, string parameterName)
	{
		Id = new(a, b, c, d, e, f, g, h, i, j, k);
		Name = name;
		ParameterName = parameterName;
	}

	public Guid Id { get; }
	public string Name { get; }
	public string ParameterName { get; }
}
