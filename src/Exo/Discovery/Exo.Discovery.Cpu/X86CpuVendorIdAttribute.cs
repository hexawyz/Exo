namespace Exo.Discovery;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class X86CpuVendorIdAttribute : Attribute
{
	public X86CpuVendorIdAttribute(string manufacturerId)
	{
		ManufacturerId = manufacturerId;
	}

	public string ManufacturerId { get; }
}
