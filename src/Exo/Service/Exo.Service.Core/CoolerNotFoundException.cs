namespace Exo.Service;

public class CoolerNotFoundException : Exception
{
	public CoolerNotFoundException() : this("The requested cooler was not found on the device.")
	{
	}

	public CoolerNotFoundException(string? message) : base(message)
	{
	}
}
