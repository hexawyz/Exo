namespace Exo.Discovery;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RootComponentAttribute : Attribute
{
	public Type Key { get; }

	public RootComponentAttribute(Type key) => Key = key;
}
