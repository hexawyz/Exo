namespace Exo;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RamModuleIdAttribute : Attribute
{
	public RamModuleIdAttribute(byte manufacturerIdContinuationCodeCount, byte manufacturerIdCode, string partNumber)
	{
		ManufacturerIdContinuationCodeCount = manufacturerIdContinuationCodeCount;
		ManufacturerIdCode = manufacturerIdCode;
		PartNumber = partNumber;
	}

	public byte ManufacturerIdContinuationCodeCount { get; }
	public byte ManufacturerIdCode { get; }
	public string PartNumber { get; }
}
