namespace Exo;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RamStickAttribute : Attribute
{
	public RamStickAttribute(byte manufacturerBankNumber, byte manufacturerIndex, string partNumber)
	{
		ManufacturerCode = new(manufacturerBankNumber, manufacturerIndex);
		PartNumber = partNumber;
	}

	public JedecManufacturerCode ManufacturerCode { get; }
	public string PartNumber { get; }
}
