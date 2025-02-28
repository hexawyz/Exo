namespace Exo.Discovery;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DnsSdServiceTypeAttribute : Attribute
{
	public string ServiceType { get; }

	public DnsSdServiceTypeAttribute(string serviceType)
	{
		ArgumentException.ThrowIfNullOrEmpty(serviceType);
		ServiceType = serviceType;
	}
}
