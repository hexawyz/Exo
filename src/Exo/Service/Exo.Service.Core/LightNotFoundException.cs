namespace Exo.Service;

public class LightNotFoundException : Exception
{
	public LightNotFoundException() : this("The requested light was not found on the device.")
	{
	}

	public LightNotFoundException(string? message) : base(message)
	{
	}
}
