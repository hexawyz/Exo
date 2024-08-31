namespace Exo.SystemManagementBus;

public class SystemManagementBusException : Exception
{
	public SystemManagementBusException()
		: this("An error occurred while accessing the SMBus device.")
	{
	}

	public SystemManagementBusException(string? message) : base(message)
	{
	}

	public SystemManagementBusException(string? message, Exception? innerException) : base(message, innerException)
	{
	}
}
