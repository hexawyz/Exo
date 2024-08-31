namespace Exo;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public abstract class DiscoverySubsystemAttribute : Attribute
{
	public abstract Type SubsystemType { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DiscoverySubsystemAttribute<TSubsystem> : DiscoverySubsystemAttribute
{
	public override Type SubsystemType => typeof(TSubsystem);
}
