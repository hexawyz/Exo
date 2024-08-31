namespace Exo.Service;

public class DeviceDisconnectedException : Exception
{
	public DeviceDisconnectedException() : this("The device has been disconnected.")
	{
	}

	public DeviceDisconnectedException(string? message) : base(message)
	{
	}
}
