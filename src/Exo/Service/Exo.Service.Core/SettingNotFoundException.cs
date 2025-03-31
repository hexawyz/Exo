namespace Exo.Service;

public class SettingNotFoundException : Exception
{
	public SettingNotFoundException() : this("The requested setting was not found on the device.")
	{
	}

	public SettingNotFoundException(string? message) : base(message)
	{
	}
}
