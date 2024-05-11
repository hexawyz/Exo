namespace Exo.Devices.Corsair.PowerSupplies;

internal class CorsairLinkException : Exception
{
	public CorsairLinkException(string message) : base (message) { }
}

internal class CorsairLinkWriteErrorException : CorsairLinkException
{
	public CorsairLinkWriteErrorException() : this("The write command failed.") { }

	public CorsairLinkWriteErrorException(string message) : base(message) { }
}

internal class CorsairLinkReadErrorException : CorsairLinkException
{
	public CorsairLinkReadErrorException() : this("The read command failed.") { }

	public CorsairLinkReadErrorException(string message) : base(message) { }
}
