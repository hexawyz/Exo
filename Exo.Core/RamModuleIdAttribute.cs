namespace Exo;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RamModuleIdAttribute : Attribute
{
	public RamModuleIdAttribute(byte manufacturerBankNumber, byte manufacturerIndex, string partNumber)
	{
		ManufacturerCode = new(manufacturerBankNumber, manufacturerIndex);
		PartNumber = partNumber;
	}

	public JedecManufacturerCode ManufacturerCode { get; }
	public string PartNumber { get; }
}
