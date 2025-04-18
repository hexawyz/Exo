namespace Exo.Service;

public class ImageNotFoundException : Exception
{
	public ImageNotFoundException() : this("The requested image was not found.")
	{
	}

	public ImageNotFoundException(string? message) : base(message)
	{
	}
}
